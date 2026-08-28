package store

import (
	"database/sql"
	"errors"
	"fmt"

	_ "github.com/mattn/go-sqlite3"
)

var ErrNotFound = errors.New("issue not found")

type Position struct {
	X float64 `json:"x"`
	Y float64 `json:"y"`
	Z float64 `json:"z"`
}

type Issue struct {
	ID            string   `json:"id"`
	Title         string   `json:"title"`
	Description   string   `json:"description"`
	Priority      string   `json:"priority"`
	Status        string   `json:"status"`
	Position      Position `json:"position"`
	SubmitterID   string   `json:"submitterId"`
	SubmitterName string   `json:"submitterName"`
	CreatedAt     string   `json:"createdAt"`
	UpdatedAt     string   `json:"updatedAt"`
}

type Store struct {
	db *sql.DB
}

func Open(path string) (*Store, error) {
	dsn := fmt.Sprintf("file:%s?_busy_timeout=5000&_fk=1", path)
	db, err := sql.Open("sqlite3", dsn)
	if err != nil {
		return nil, err
	}
	db.SetMaxOpenConns(1)
	if err := db.Ping(); err != nil {
		_ = db.Close()
		return nil, err
	}
	if _, err := db.Exec(`
CREATE TABLE IF NOT EXISTS issues (
	id TEXT PRIMARY KEY,
	title TEXT NOT NULL,
	description TEXT NOT NULL DEFAULT '',
	priority TEXT NOT NULL,
	status TEXT NOT NULL,
	pos_x REAL NOT NULL,
	pos_y REAL NOT NULL,
	pos_z REAL NOT NULL,
	submitter_id TEXT NOT NULL DEFAULT '',
	submitter_name TEXT NOT NULL DEFAULT '未知',
	created_at TEXT NOT NULL,
	updated_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_issues_created_at ON issues(created_at);
`); err != nil {
		_ = db.Close()
		return nil, err
	}
	s := &Store{db: db}
	if err := s.migrateIssueSubmitter(); err != nil {
		_ = db.Close()
		return nil, err
	}
	if err := s.ensureUsersTable(); err != nil {
		_ = db.Close()
		return nil, err
	}
	return s, nil
}

func (s *Store) migrateIssueSubmitter() error {
	cols, err := s.issueColumns()
	if err != nil {
		return err
	}
	if !cols["submitter_id"] {
		if _, err := s.db.Exec(`ALTER TABLE issues ADD COLUMN submitter_id TEXT NOT NULL DEFAULT ''`); err != nil {
			return err
		}
	}
	if !cols["submitter_name"] {
		if _, err := s.db.Exec(`ALTER TABLE issues ADD COLUMN submitter_name TEXT NOT NULL DEFAULT '未知'`); err != nil {
			return err
		}
	}
	_, err = s.db.Exec(`UPDATE issues SET submitter_name = '未知' WHERE submitter_name IS NULL OR trim(submitter_name) = ''`)
	return err
}

func (s *Store) issueColumns() (map[string]bool, error) {
	rows, err := s.db.Query(`PRAGMA table_info(issues)`)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	cols := map[string]bool{}
	for rows.Next() {
		var cid int
		var name, ctype string
		var notnull, pk int
		var dflt sql.NullString
		if err := rows.Scan(&cid, &name, &ctype, &notnull, &dflt, &pk); err != nil {
			return nil, err
		}
		cols[name] = true
	}
	return cols, rows.Err()
}

func (s *Store) Close() error {
	if s == nil || s.db == nil {
		return nil
	}
	return s.db.Close()
}

func (s *Store) Create(issue Issue) error {
	_, err := s.db.Exec(
		`INSERT INTO issues (id, title, description, priority, status, pos_x, pos_y, pos_z, submitter_id, submitter_name, created_at, updated_at)
		 VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
		issue.ID, issue.Title, issue.Description, issue.Priority, issue.Status,
		issue.Position.X, issue.Position.Y, issue.Position.Z,
		issue.SubmitterID, issue.SubmitterName,
		issue.CreatedAt, issue.UpdatedAt,
	)
	return err
}

const issueSelect = `id, title, description, priority, status, pos_x, pos_y, pos_z, submitter_id, submitter_name, created_at, updated_at`

func (s *Store) List() ([]Issue, error) {
	rows, err := s.db.Query(`SELECT ` + issueSelect + ` FROM issues ORDER BY created_at DESC`)
	if err != nil {
		return nil, err
	}
	defer rows.Close()

	issues := make([]Issue, 0)
	for rows.Next() {
		issue, err := scanIssue(rows)
		if err != nil {
			return nil, err
		}
		issues = append(issues, issue)
	}
	return issues, rows.Err()
}

func (s *Store) Get(id string) (*Issue, error) {
	row := s.db.QueryRow(`SELECT `+issueSelect+` FROM issues WHERE id = ?`, id)
	issue, err := scanIssue(row)
	if errors.Is(err, sql.ErrNoRows) {
		return nil, ErrNotFound
	}
	if err != nil {
		return nil, err
	}
	return &issue, nil
}

func (s *Store) UpdateFields(id, title, description, priority, updatedAt string) (*Issue, error) {
	res, err := s.db.Exec(
		`UPDATE issues SET title = ?, description = ?, priority = ?, updated_at = ? WHERE id = ?`,
		title, description, priority, updatedAt, id,
	)
	if err != nil {
		return nil, err
	}
	n, err := res.RowsAffected()
	if err != nil {
		return nil, err
	}
	if n == 0 {
		return nil, ErrNotFound
	}
	return s.Get(id)
}

func (s *Store) UpdateStatus(id, status, updatedAt string) (*Issue, error) {
	res, err := s.db.Exec(`UPDATE issues SET status = ?, updated_at = ? WHERE id = ?`, status, updatedAt, id)
	if err != nil {
		return nil, err
	}
	n, err := res.RowsAffected()
	if err != nil {
		return nil, err
	}
	if n == 0 {
		return nil, ErrNotFound
	}
	return s.Get(id)
}

func (s *Store) Delete(id string) error {
	res, err := s.db.Exec(`DELETE FROM issues WHERE id = ?`, id)
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

type scanner interface {
	Scan(dest ...any) error
}

func scanIssue(sc scanner) (Issue, error) {
	var issue Issue
	err := sc.Scan(
		&issue.ID, &issue.Title, &issue.Description, &issue.Priority, &issue.Status,
		&issue.Position.X, &issue.Position.Y, &issue.Position.Z,
		&issue.SubmitterID, &issue.SubmitterName,
		&issue.CreatedAt, &issue.UpdatedAt,
	)
	return issue, err
}
