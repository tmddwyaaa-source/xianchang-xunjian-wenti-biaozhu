package api

import (
	"net/http"
	"net/http/httptest"
	"testing"
)

func TestWithCORS_LocalhostAnyPort(t *testing.T) {
	h := withCORS(http.HandlerFunc(func(w http.ResponseWriter, _ *http.Request) {
		w.WriteHeader(http.StatusOK)
	}))

	req := httptest.NewRequest(http.MethodOptions, "/api/issues", nil)
	req.Header.Set("Origin", "http://localhost:5174")
	req.Header.Set("Access-Control-Request-Method", "PUT")
	req.Header.Set("Access-Control-Request-Headers", "Authorization")
	rec := httptest.NewRecorder()
	h.ServeHTTP(rec, req)

	if rec.Code != http.StatusNoContent {
		t.Fatalf("status = %d, want 204", rec.Code)
	}
	if got := rec.Header().Get("Access-Control-Allow-Origin"); got != "http://localhost:5174" {
		t.Fatalf("Access-Control-Allow-Origin = %q, want echoed Origin", got)
	}
	if got := rec.Header().Get("Access-Control-Allow-Methods"); got != "GET, POST, PATCH, PUT, DELETE, OPTIONS" {
		t.Fatalf("Access-Control-Allow-Methods = %q", got)
	}
	if got := rec.Header().Get("Access-Control-Allow-Headers"); got != "Content-Type, Authorization" {
		t.Fatalf("Access-Control-Allow-Headers = %q", got)
	}
}

func TestIsAllowedOrigin(t *testing.T) {
	cases := []struct {
		origin string
		want   bool
	}{
		{"http://localhost:5174", true},
		{"http://127.0.0.1:5174", true},
		{"http://localhost:5173", true},
		{"http://127.0.0.1:8080", true},
		{"http://localhost", true},
		{"https://localhost:5174", false},
		{"http://example.com:5174", false},
		{"http://192.168.2.14:5174", false},
		{"", false},
	}
	for _, tc := range cases {
		if got := isAllowedOrigin(tc.origin); got != tc.want {
			t.Errorf("isAllowedOrigin(%q) = %v, want %v", tc.origin, got, tc.want)
		}
	}
}
