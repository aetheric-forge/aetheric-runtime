import uuid
from contextlib import asynccontextmanager
from fastapi import FastAPI, Body, Request
from .discovery import discover_commands
from .rabbit import publish_command

@asynccontextmanager
async def lifespan(app: FastAPI):
    # 🔹 This runs at startup
    keys = await discover_commands()

    for key in keys:
        segments = key.split(".")
        path = "/api/commands/" + "/".join(segments)

        async def handler(
                request: Request,
                body: dict = Body(...),
                _routing_key: str = key,   # default arg captures key correctly
        ):
            session_id = request.headers.get("X-Session-Id") or "anonymous"
            correlation_id = str(uuid.uuid4())

            envelope = {
                "meta": {
                    "session_id": session_id,
                    "reply_channel": f"session.{session_id}",
                    "correlation_id": correlation_id,
                    "routing_key": _routing_key,
                },
                "body": body,
            }

            await publish_command(_routing_key, envelope)

            return {
                "status": "queued",
                "routing_key": _routing_key,
                "correlation_id": correlation_id,
                "session_id": session_id,
            }

        app.add_api_route(
            path,
            handler,
            name=f"cmd:{key}",
            methods=["POST"],
            summary=f"Proxy for {key}",
        )

    # Hand control back to FastAPI – app is now “running”
    yield

    # 🔹 This runs at shutdown (optional cleanup)
    # e.g. close RabbitMQ connection if you keep a global one
    # await close_connection_if_any()
