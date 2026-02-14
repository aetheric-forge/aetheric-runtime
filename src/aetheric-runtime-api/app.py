from fastapi import FastAPI, Body, Request
from fastapi.websockets import WebSocket
from .websocket_hub import websocket_session
from .lifespan import lifespan

app = FastAPI(title="Parallel You API", lifespan=lifespan)

@app.websocket("/ws")
async def ws_endpoint(ws: WebSocket, session_id: str):
    await websocket_session(ws, session_id)

from fastapi.routing import APIRoute, APIWebSocketRoute

print("=== ROUTES REGISTERED ===")
for r in app.routes:
    if isinstance(r, APIRoute):
        print("HTTP ", r.path, r.methods)
    if isinstance(r, APIWebSocketRoute):
        print("WS   ", r.path)
print("=== END ROUTES ===")
