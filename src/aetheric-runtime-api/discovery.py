import httpx

async def discover_commands() -> list[str]:
    """Return a sorted list of routing keys bound to the commands exchange."""
    base = "http://localhost:15672/api"

    async with httpx.AsyncClient(base_url=base, auth=("py", "py")) as client:
        r = await client.get("/bindings/%2F")
        r.raise_for_status()
        bindings = r.json()

    keys = {b["routing_key"] for b in bindings if (b["routing_key"] is not None and b["source"] == "parallel_you.commands")}
    return sorted(keys)
