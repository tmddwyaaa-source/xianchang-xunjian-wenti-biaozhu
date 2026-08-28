package api

import (
	"crypto/rand"
	"encoding/json"
	"errors"
	"fmt"
	"net/http"
	"net/url"
	"strings"
	"time"

	"github.com/gorilla/websocket"

	"inspect/internal/auth"
	"inspect/internal/store"
	"inspect/internal/ws"
)

var (
	validPriority = map[string]bool{"low": true, "medium": true, "high": true}
	validStatus   = map[string]bool{"open": true, "in_progress": true, "resolved": true}
)

const (
	corsAllowMethods = "GET, POST, PATCH, PUT, DELETE, OPTIONS"
	corsAllowHeaders = "Content-Type, Authorization"
)

var upgrader = websocket.Upgrader{
	CheckOrigin: func(r *http.Request) bool {
		origin := r.Header.Get("Origin")
		if origin == "" {
			return true
		}
		return isAllowedOrigin(origin)
	},
}

type Handler struct {
	store *store.Store
	auth  *auth.Service
	hub   *ws.Hub
}

func New(st *store.Store, tokens *auth.Service) http.Handler {
	hub := ws.NewHub()
	go hub.Run()
	h := &Handler{store: st, auth: tokens, hub: hub}
	mux := http.NewServeMux()
	mux.HandleFunc("GET /health", h.health)
	mux.HandleFunc("POST /api/auth/login", h.login)
	mux.HandleFunc("POST /api/auth/password", h.requireAuth(h.changePassword))
	mux.HandleFunc("GET /ws", h.serveWS)
	mux.HandleFunc("POST /api/issues", h.requireAuth(h.createIssue))
	mux.HandleFunc("GET /api/issues", h.requireAuth(h.listIssues))
	mux.HandleFunc("PATCH /api/issues/{id}", h.requireAuth(h.patchIssue))
	mux.HandleFunc("PUT /api/issues/{id}", h.requireAuth(h.putIssue))
	mux.HandleFunc("DELETE /api/issues/{id}", h.requireAuth(h.deleteIssue))
	return withCORS(mux)
}

func withCORS(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		origin := r.Header.Get("Origin")
		if isAllowedOrigin(origin) {
			w.Header().Set("Access-Control-Allow-Origin", origin)
			w.Header().Set("Vary", "Origin")
			w.Header().Set("Access-Control-Allow-Methods", corsAllowMethods)
			w.Header().Set("Access-Control-Allow-Headers", corsAllowHeaders)
		}
		if r.Method == http.MethodOptions {
			w.WriteHeader(http.StatusNoContent)
			return
		}
		next.ServeHTTP(w, r)
	})
}

// isAllowedOrigin allows any http Origin whose host is localhost or 127.0.0.1 (any port).
func isAllowedOrigin(origin string) bool {
	u, err := url.Parse(origin)
	if err != nil || u.Scheme != "http" {
		return false
	}
	host := strings.ToLower(u.Hostname())
	return host == "localhost" || host == "127.0.0.1"
}

func (h *Handler) requireAuth(next http.HandlerFunc) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		raw, ok := auth.Bearer(r.Header.Get("Authorization"))
		if !ok {
			writeError(w, http.StatusUnauthorized, "unauthorized")
			return
		}
		claims, err := h.auth.Parse(raw)
		if err != nil {
			writeError(w, http.StatusUnauthorized, "unauthorized")
			return
		}
		next(w, r.WithContext(auth.WithClaims(r.Context(), claims)))
	}
}

func claimsOf(r *http.Request) *auth.Claims {
	return auth.ClaimsFrom(r.Context())
}

func (h *Handler) health(w http.ResponseWriter, _ *http.Request) {
	writeJSON(w, http.StatusOK, map[string]bool{"ok": true})
}

type loginBody struct {
	Username *string `json:"username"`
	Password *string `json:"password"`
}

type publicUser struct {
	ID       string `json:"id"`
	Username string `json:"username"`
	Role     string `json:"role"`
}

func (h *Handler) login(w http.ResponseWriter, r *http.Request) {
	var body loginBody
	if err := decodeJSON(r, &body); err != nil {
		writeError(w, http.StatusBadRequest, "invalid json")
		return
	}
	username := ""
	if body.Username != nil {
		username = strings.TrimSpace(*body.Username)
	}
	password := ""
	if body.Password != nil {
		password = *body.Password
	}
	if username == "" {
		writeError(w, http.StatusBadRequest, "username is required")
		return
	}
	if password == "" {
		writeError(w, http.StatusBadRequest, "password is required")
		return
	}
	user, err := h.store.GetUserByUsername(username)
	if err != nil || !auth.CheckPassword(user.PasswordHash, password) {
		writeError(w, http.StatusUnauthorized, "invalid credentials")
		return
	}
	token, err := h.auth.Sign(user.ID, user.Username, user.Role)
	if err != nil {
		writeError(w, http.StatusInternalServerError, "internal server error")
		return
	}
	writeJSON(w, http.StatusOK, map[string]any{
		"token": token,
		"user": publicUser{
			ID:       user.ID,
			Username: user.Username,
			Role:     user.Role,
		},
	})
}

type changePasswordBody struct {
	OldPassword *string `json:"oldPassword"`
	NewPassword *string `json:"newPassword"`
}

func (h *Handler) changePassword(w http.ResponseWriter, r *http.Request) {
	var body changePasswordBody
	if err := decodeJSON(r, &body); err != nil {
		writeError(w, http.StatusBadRequest, "invalid json")
		return
	}
	oldPassword := ""
	if body.OldPassword != nil {
		oldPassword = *body.OldPassword
	}
	newPassword := ""
	if body.NewPassword != nil {
		newPassword = *body.NewPassword
	}
	if oldPassword == "" {
		writeError(w, http.StatusBadRequest, "oldPassword is required")
		return
	}
	if strings.TrimSpace(newPassword) == "" {
		writeError(w, http.StatusBadRequest, "newPassword is required")
		return
	}
	newPassword = strings.TrimSpace(newPassword)
	if len(newPassword) < 6 {
		writeError(w, http.StatusBadRequest, "password too short")
		return
	}
	if newPassword == oldPassword {
		writeError(w, http.StatusBadRequest, "password unchanged")
		return
	}

	c := claimsOf(r)
	user, err := h.store.GetUserByID(c.Subject)
	if err != nil {
		writeError(w, http.StatusUnauthorized, "unauthorized")
		return
	}
	if !auth.CheckPassword(user.PasswordHash, oldPassword) {
		writeError(w, http.StatusBadRequest, "invalid old password")
		return
	}
	hash, err := auth.HashPassword(newPassword)
	if err != nil {
		writeError(w, http.StatusInternalServerError, "internal server error")
		return
	}
	if err := h.store.UpdatePassword(user.ID, hash); err != nil {
		writeError(w, http.StatusInternalServerError, "internal server error")
		return
	}
	writeJSON(w, http.StatusOK, map[string]bool{"ok": true})
}

type createBody struct {
	Title       *string       `json:"title"`
	Description *string       `json:"description"`
	Priority    *string       `json:"priority"`
	Position    *positionBody `json:"position"`
}

type positionBody struct {
	X *float64 `json:"x"`
	Y *float64 `json:"y"`
	Z *float64 `json:"z"`
}

type patchBody struct {
	Status *string `json:"status"`
}

type putBody struct {
	Title       *string `json:"title"`
	Description *string `json:"description"`
	Priority    *string `json:"priority"`
}

func (h *Handler) createIssue(w http.ResponseWriter, r *http.Request) {
	c := claimsOf(r)
	if c.Role != "admin" && c.Role != "inspector" {
		writeError(w, http.StatusForbidden, "forbidden")
		return
	}

	var body createBody
	if err := decodeJSON(r, &body); err != nil {
		writeError(w, http.StatusBadRequest, "invalid json")
		return
	}

	title := ""
	if body.Title != nil {
		title = strings.TrimSpace(*body.Title)
	}
	if title == "" {
		writeError(w, http.StatusBadRequest, "title is required")
		return
	}

	priority := ""
	if body.Priority != nil {
		priority = *body.Priority
	}
	if !validPriority[priority] {
		writeError(w, http.StatusBadRequest, "invalid priority")
		return
	}

	if body.Position == nil || body.Position.X == nil || body.Position.Y == nil || body.Position.Z == nil {
		writeError(w, http.StatusBadRequest, "position is required")
		return
	}

	desc := ""
	if body.Description != nil {
		desc = *body.Description
	}

	id, err := newID()
	if err != nil {
		writeError(w, http.StatusInternalServerError, "internal server error")
		return
	}
	now := nowUTC()
	issue := store.Issue{
		ID:          id,
		Title:       title,
		Description: desc,
		Priority:    priority,
		Status:      "open",
		Position: store.Position{
			X: *body.Position.X,
			Y: *body.Position.Y,
			Z: *body.Position.Z,
		},
		SubmitterID:   c.Subject,
		SubmitterName: c.Username,
		CreatedAt:     now,
		UpdatedAt:     now,
	}
	if err := h.store.Create(issue); err != nil {
		writeError(w, http.StatusInternalServerError, "internal server error")
		return
	}
	writeJSON(w, http.StatusCreated, issue)
	h.emit("issue.created", &issue, "")
}

func (h *Handler) listIssues(w http.ResponseWriter, _ *http.Request) {
	issues, err := h.store.List()
	if err != nil {
		writeError(w, http.StatusInternalServerError, "internal server error")
		return
	}
	writeJSON(w, http.StatusOK, map[string][]store.Issue{"issues": issues})
}

func (h *Handler) patchIssue(w http.ResponseWriter, r *http.Request) {
	if claimsOf(r).Role != "admin" {
		writeError(w, http.StatusForbidden, "forbidden")
		return
	}
	id := r.PathValue("id")
	if id == "" {
		writeError(w, http.StatusBadRequest, "invalid id")
		return
	}
	var body patchBody
	if err := decodeJSON(r, &body); err != nil {
		writeError(w, http.StatusBadRequest, "invalid json")
		return
	}
	if body.Status == nil {
		writeError(w, http.StatusBadRequest, "status is required")
		return
	}
	if !validStatus[*body.Status] {
		writeError(w, http.StatusBadRequest, "invalid status")
		return
	}
	issue, err := h.store.UpdateStatus(id, *body.Status, nowUTC())
	if errors.Is(err, store.ErrNotFound) {
		writeError(w, http.StatusNotFound, "issue not found")
		return
	}
	if err != nil {
		writeError(w, http.StatusInternalServerError, "internal server error")
		return
	}
	writeJSON(w, http.StatusOK, issue)
	h.emit("issue.updated", issue, "")
}

func (h *Handler) putIssue(w http.ResponseWriter, r *http.Request) {
	id := r.PathValue("id")
	if id == "" {
		writeError(w, http.StatusBadRequest, "invalid id")
		return
	}
	issue, err := h.store.Get(id)
	if errors.Is(err, store.ErrNotFound) {
		writeError(w, http.StatusNotFound, "issue not found")
		return
	}
	if err != nil {
		writeError(w, http.StatusInternalServerError, "internal server error")
		return
	}
	c := claimsOf(r)
	if c.Role == "viewer" || (c.Role != "admin" && issue.SubmitterID != c.Subject) {
		writeError(w, http.StatusForbidden, "forbidden")
		return
	}

	var body putBody
	if err := decodeJSON(r, &body); err != nil {
		writeError(w, http.StatusBadRequest, "invalid json")
		return
	}
	if body.Title == nil && body.Description == nil && body.Priority == nil {
		writeError(w, http.StatusBadRequest, "at least one field is required")
		return
	}
	if body.Title != nil {
		title := strings.TrimSpace(*body.Title)
		if title == "" {
			writeError(w, http.StatusBadRequest, "title is required")
			return
		}
		issue.Title = title
	}
	if body.Description != nil {
		issue.Description = *body.Description
	}
	if body.Priority != nil {
		if !validPriority[*body.Priority] {
			writeError(w, http.StatusBadRequest, "invalid priority")
			return
		}
		issue.Priority = *body.Priority
	}

	updated, err := h.store.UpdateFields(issue.ID, issue.Title, issue.Description, issue.Priority, nowUTC())
	if errors.Is(err, store.ErrNotFound) {
		writeError(w, http.StatusNotFound, "issue not found")
		return
	}
	if err != nil {
		writeError(w, http.StatusInternalServerError, "internal server error")
		return
	}
	writeJSON(w, http.StatusOK, updated)
	h.emit("issue.updated", updated, "")
}

func (h *Handler) deleteIssue(w http.ResponseWriter, r *http.Request) {
	id := r.PathValue("id")
	if id == "" {
		writeError(w, http.StatusBadRequest, "invalid id")
		return
	}
	issue, err := h.store.Get(id)
	if errors.Is(err, store.ErrNotFound) {
		writeError(w, http.StatusNotFound, "issue not found")
		return
	}
	if err != nil {
		writeError(w, http.StatusInternalServerError, "internal server error")
		return
	}
	c := claimsOf(r)
	if c.Role != "admin" && issue.SubmitterID != c.Subject {
		writeError(w, http.StatusForbidden, "forbidden")
		return
	}
	if err := h.store.Delete(id); err != nil {
		writeError(w, http.StatusInternalServerError, "internal server error")
		return
	}
	w.WriteHeader(http.StatusNoContent)
	h.emit("issue.deleted", nil, id)
}

func (h *Handler) serveWS(w http.ResponseWriter, r *http.Request) {
	token := strings.TrimSpace(r.URL.Query().Get("token"))
	if token == "" {
		writeError(w, http.StatusUnauthorized, "unauthorized")
		return
	}
	if _, err := h.auth.Parse(token); err != nil {
		writeError(w, http.StatusUnauthorized, "unauthorized")
		return
	}
	conn, err := upgrader.Upgrade(w, r, nil)
	if err != nil {
		return
	}
	client := ws.NewClient(h.hub, conn)
	h.hub.Register(client)
	go client.WritePump()
	go client.ReadPump()
}

func (h *Handler) emit(typ string, issue *store.Issue, id string) {
	payload := map[string]any{"type": typ}
	if issue != nil {
		payload["issue"] = issue
	}
	if id != "" {
		payload["id"] = id
	}
	h.hub.Broadcast(payload)
}

func decodeJSON(r *http.Request, dst any) error {
	dec := json.NewDecoder(r.Body)
	if err := dec.Decode(dst); err != nil {
		return err
	}
	return nil
}

func writeJSON(w http.ResponseWriter, status int, v any) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	enc := json.NewEncoder(w)
	enc.SetEscapeHTML(false)
	_ = enc.Encode(v)
}

func writeError(w http.ResponseWriter, status int, msg string) {
	writeJSON(w, status, map[string]string{"error": msg})
}

func nowUTC() string {
	return time.Now().UTC().Format("2006-01-02T15:04:05.000Z")
}

func newID() (string, error) {
	var b [16]byte
	if _, err := rand.Read(b[:]); err != nil {
		return "", err
	}
	b[6] = (b[6] & 0x0f) | 0x40
	b[8] = (b[8] & 0x3f) | 0x80
	return fmt.Sprintf("%x-%x-%x-%x-%x", b[0:4], b[4:6], b[6:8], b[8:10], b[10:]), nil
}
