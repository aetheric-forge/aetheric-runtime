import os
import json
import aio_pika
from typing import Any, Dict

RABBITMQ_URL = os.getenv("RABBITMQ_URL")
if RABBITMQ_URL is None:
    raise ValueError("RABBITMQ_URL environment variable not set")
COMMAND_EXCHANGE = "parallel_you.commands"

_connection = None

async def get_connection():
    global _connection
    if _connection is None or _connection.is_closed:
        _connection = await aio_pika.connect_robust(RABBITMQ_URL)
    return _connection


async def publish_command(routing_key: str, envelope: Dict[str, Any]):
    conn = await get_connection()
    channel = await conn.channel()
    exchange = await channel.declare_exchange(
        COMMAND_EXCHANGE,
        aio_pika.ExchangeType.TOPIC,
        durable=True
    )

    await exchange.publish(
        aio_pika.Message(
            body=json.dumps(envelope).encode("utf-8"),
            content_type="application/json",
            correlation_id=envelope["meta"]["correlation_id"],
        ),
        routing_key=routing_key,
    )

