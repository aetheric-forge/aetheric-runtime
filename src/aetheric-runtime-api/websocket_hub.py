import os
import aio_pika
from fastapi import WebSocket
from fastapi.params import Query

EVENT_EXCHANGE = "parallel_you.events"
RABBITMQ_URL = os.getenv("RABBITMQ_URL")
if RABBITMQ_URL is None:
    raise ValueError("RABBITMQ_URL environment variable not set.")

async def websocket_session(ws: WebSocket, session_id: str = Query(...)):
    await ws.accept()

    conn = await aio_pika.connect_robust(RABBITMQ_URL)
    channel = await conn.channel()

    exchange = await channel.declare_exchange(
        EVENT_EXCHANGE,
        aio_pika.ExchangeType.TOPIC,
        durable=True,
    )

    # each session gets a temporary queue
    queue = await channel.declare_queue(exclusive=True)
    await queue.bind(exchange, routing_key=f"session.{session_id}.#")

    async with queue.iterator() as q:
        async for message in q:
            async with message.process():
                await ws.send_text(message.body.decode("utf-8"))
