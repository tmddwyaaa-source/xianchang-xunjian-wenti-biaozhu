package auth

import (
	"context"
	"errors"
	"fmt"
	"strings"
	"time"

	"github.com/golang-jwt/jwt/v5"
	"golang.org/x/crypto/bcrypt"
)

const TokenTTL = 24 * time.Hour

// DevSecret is used only when JWT_SECRET is unset. Log a warning at startup.
const DevSecret = "inspect-dev-jwt-secret-localhost-only"

var ErrUnauthorized = errors.New("unauthorized")

type Claims struct {
	Username string `json:"username"`
	Role     string `json:"role"`
	jwt.RegisteredClaims
}

type Service struct {
	secret []byte
}

func New(secret string) *Service {
	if secret == "" {
		secret = DevSecret
	}
	return &Service{secret: []byte(secret)}
}

func (s *Service) Sign(userID, username, role string) (string, error) {
	now := time.Now()
	tok := jwt.NewWithClaims(jwt.SigningMethodHS256, Claims{
		Username: username,
		Role:     role,
		RegisteredClaims: jwt.RegisteredClaims{
			Subject:   userID,
			ExpiresAt: jwt.NewNumericDate(now.Add(TokenTTL)),
			IssuedAt:  jwt.NewNumericDate(now),
		},
	})
	return tok.SignedString(s.secret)
}

func (s *Service) Parse(token string) (*Claims, error) {
	parsed, err := jwt.ParseWithClaims(token, &Claims{}, func(t *jwt.Token) (any, error) {
		if t.Method != jwt.SigningMethodHS256 {
			return nil, fmt.Errorf("unexpected signing method")
		}
		return s.secret, nil
	})
	if err != nil || parsed == nil || !parsed.Valid {
		return nil, ErrUnauthorized
	}
	claims, ok := parsed.Claims.(*Claims)
	if !ok || claims.Subject == "" {
		return nil, ErrUnauthorized
	}
	return claims, nil
}

func Bearer(header string) (string, bool) {
	const prefix = "Bearer "
	if !strings.HasPrefix(header, prefix) {
		return "", false
	}
	tok := strings.TrimSpace(header[len(prefix):])
	if tok == "" {
		return "", false
	}
	return tok, true
}

func HashPassword(plain string) (string, error) {
	b, err := bcrypt.GenerateFromPassword([]byte(plain), bcrypt.DefaultCost)
	if err != nil {
		return "", err
	}
	return string(b), nil
}

func CheckPassword(hash, plain string) bool {
	return bcrypt.CompareHashAndPassword([]byte(hash), []byte(plain)) == nil
}

type ctxKey struct{}

func WithClaims(ctx context.Context, c *Claims) context.Context {
	return context.WithValue(ctx, ctxKey{}, c)
}

func ClaimsFrom(ctx context.Context) *Claims {
	c, _ := ctx.Value(ctxKey{}).(*Claims)
	return c
}
