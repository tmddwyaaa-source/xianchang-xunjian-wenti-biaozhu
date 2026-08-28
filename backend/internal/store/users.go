package store

import (
	"database/sql"
	"crypto/rand"
	"errors"
	"fmt"

	"inspect/internal/auth"
)

type User struct {
	ID           string
	Username     string
	PasswordHash string
	Role         string
}

func (s *Store) ensureUsersTable() error {
	_, err := s.db.Exec(`
CREATE TABLE IF NOT EXISTS users (
	id TEXT PRIMARY KEY,
	username TEXT NOT NULL UNIQUE,
	password TEXT NOT NULL,
	role TEXT NOT NULL
);`)
	return err
}

func (s *Store) SeedUsersIfEmpty() error {
	var n int
	if err := s.db.QueryRow(`SELECT COUNT(*) FROM users`).Scan(&n); err != nil {
		return err
	}
	if n > 0 {
		return nil
	}
	seeds := []struct {
		username, password, role string
	}{
		{"admin", "admin123", "admin"},
		{"inspector", "inspect123", "inspector"},
		{"viewer", "view123", "viewer"},
	}
	for _, seed := range seeds {
		hash, err := auth.HashPassword(seed.password)
		if err != nil {
			return err
		}
		id, err := newID()
		if err != nil {
			return err
		}
		if _, err := s.db.Exec(
			`INSERT INTO users (id, username, password, role) VALUES (?, ?, ?, ?)`,
			id, seed.username, hash, seed.role,
		); err != nil {
			return err
		}
	}
	return nil
}

func (s *Store) GetUserByUsername(username string) (*User, error) {
	return s.scanUser(s.db.QueryRow(`SELECT id, username, password, role FROM users WHERE username = ?`, username))
}

func (s *Store) GetUserByID(id string) (*User, error) {
	return s.scanUser(s.db.QueryRow(`SELECT id, username, password, role FROM users WHERE id = ?`, id))
}

func (s *Store) UpdatePassword(id, hash string) error {
	res, err := s.db.Exec(`UPDATE users SET password = ? WHERE id = ?`, hash, id)
	if err != nil {
		return err
	}
	n, err := res.RowsAffected()
	if err != nil {
		return err
	}
	if n == 0 {
		return ErrNotFound
	}
	return nil
}

func (s *Store) scanUser(row *sql.Row) (*User, error) {
	var u User
	if err := row.Scan(&u.ID, &u.Username, &u.PasswordHash, &u.Role); err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return nil, ErrNotFound
		}
		return nil, err
	}
	return &u, nil
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
