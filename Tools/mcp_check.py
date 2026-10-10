import asyncio, json, sys
from fastmcp import Client

async def main():
    async with Client('http://127.0.0.1:8080/mcp') as client:
        if len(sys.argv)>1 and sys.argv[1]=='resource':
            print(await client.read_resource(sys.argv[2]))
        elif len(sys.argv)>1 and sys.argv[1]=='schema':
            print(next(t for t in await client.list_tools() if t.name==sys.argv[2]).model_dump_json(indent=2))
        elif len(sys.argv) > 1:
            result = await client.call_tool(sys.argv[1], json.loads(sys.argv[2]) if len(sys.argv)>2 else {})
            print(result)
        else:
            tools = await client.list_tools()
            print('MCP handshake OK; tools:', ', '.join(t.name for t in tools))
            resources = await client.list_resources()
            print('Resources:', [(str(r.uri), r.name) for r in resources])

asyncio.run(main())
