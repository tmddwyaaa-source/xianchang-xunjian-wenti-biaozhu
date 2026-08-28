package main

import (
	"log"
	"net/http"
	"os"
	"path/filepath"

	"inspect/internal/api"
	"inspect/internal/auth"
	"inspect/internal/store"
)

func main() {
	dbPath := resolveDBPath()
	if err := os.MkdirAll(filepath.Dir(dbPath), 0o755); err != nil {
		log.Fatalf("create data dir: %v", err)
	}

	st, err := store.Open(dbPath)
	if err != nil {
		log.Fatalf("open db: %v", err)
	}
	defer st.Close()

	if err := st.SeedUsersIfEmpty(); err != nil {
		log.Fatalf("seed users: %v", err)
	}

	secret := os.Getenv("JWT_SECRET")
	if secret == "" {
		secret = auth.DevSecret
		log.Printf("WARNING: JWT_SECRET unset; using development default (localhost only)")
	}
	tokens := auth.New(secret)

	addr := os.Getenv("LISTEN_ADDR")
	if addr == "" {
		addr = "0.0.0.0:8080"
	}
	log.Printf("listening on %s (db=%s)", addr, dbPath)
	if err := http.ListenAndServe(addr, api.New(st, tokens)); err != nil {
		log.Fatal(err)
	}
}

func resolveDBPath() string {
	if p := os.Getenv("ISSUES_DB"); p != "" {
		return p
	}
	for _, dir := range []string{"data", filepath.Join("backend", "data")} {
		if info, err := os.Stat(dir); err == nil && info.IsDir() {
			return filepath.Join(dir, "issues.db")
		}
	}
	return filepath.Join("data", "issues.db")
}
