package ws

import (
	"encoding/json"
	"time"

	"github.com/gorilla/websocket"
)

const (
	writeWait  = 10 * time.Second
	sendBuffer = 16
)

type Hub struct {
	register   chan *Client
	unregister chan *Client
	broadcast  chan []byte
	clients    map[*Client]struct{}
}

func NewHub() *Hub {
	return &Hub{
		register:   make(chan *Client),
		unregister: make(chan *Client),
		broadcast:  make(chan []byte, sendBuffer),
		clients:    make(map[*Client]struct{}),
	}
}

func (h *Hub) Run() {
	for {
		select {
		case c := <-h.register:
			h.clients[c] = struct{}{}
		case c := <-h.unregister:
			if _, ok := h.clients[c]; ok {
				delete(h.clients, c)
				close(c.send)
			}
		case msg := <-h.broadcast:
			for c := range h.clients {
				select {
				case c.send <- msg:
				default:
					// slow client: drop this frame, keep the connection
				}
			}
		}
	}
}

func (h *Hub) Register(c *Client) {
	h.register <- c
}

func (h *Hub) Broadcast(v any) {
	b, err := json.Marshal(v)
	if err != nil {
		return
	}
	h.broadcast <- b
}

type Client struct {
	hub  *Hub
	conn *websocket.Conn
	send chan []byte
}

func NewClient(hub *Hub, conn *websocket.Conn) *Client {
	return &Client{
		hub:  hub,
		conn: conn,
		send: make(chan []byte, sendBuffer),
	}
}

func (c *Client) ReadPump() {
	defer func() {
		c.hub.unregister <- c
		_ = c.conn.Close()
	}()
	c.conn.SetReadLimit(512)
	for {
		if _, _, err := c.conn.NextReader(); err != nil {
			break
		}
	}
}

func (c *Client) WritePump() {
	defer c.conn.Close()
	for msg := range c.send {
		_ = c.conn.SetWriteDeadline(time.Now().Add(writeWait))
		if err := c.conn.WriteMessage(websocket.TextMessage, msg); err != nil {
			return
		}
	}
}
