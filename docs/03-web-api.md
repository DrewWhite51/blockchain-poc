# Phase 3: Web API

## Overview

This phase creates the HTTP API endpoints that enable both peer-to-peer communication and user interaction with the blockchain node. The API serves two purposes:
1. **P2P Endpoints**: Allow nodes to communicate with each other
2. **User Endpoints**: Allow users to interact with their local node

## Goals

- Create API controller for P2P communication
- Create API controller for user interaction
- Configure ASP.NET Core web host
- Register services in dependency injection
- Enable CORS for cross-origin requests

## Architecture

```
User/Browser                    Node A API                     Node B API
     |                               |                               |
     |-- POST /api/blockchain/tx --->|                               |
     |                               |-- POST /api/node/tx --------->|
     |                               |                               |
     |<--- 200 OK ------------------|                               |
     |                               |<--- 200 OK -------------------|
```

## Step 1: Create API Controllers

### 1.1 Create Controllers Folder

```bash
mkdir Controllers
```

### 1.2 Create NodeApiController.cs (P2P Communication)

**File:** `Controllers/NodeApiController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using Models;
using Network;
using Newtonsoft.Json;
using Services;

namespace Controllers;

[ApiController]
[Route("api/node")]
public class NodeApiController : ControllerBase
{
    private readonly BlockchainNodeService _nodeService;
    private readonly P2PNode _p2pNode;
    private readonly PeerManager _peerManager;
    private readonly ConsensusManager _consensus;

    public NodeApiController(
        BlockchainNodeService nodeService,
        P2PNode p2pNode,
        PeerManager peerManager,
        ConsensusManager consensus)
    {
        _nodeService = nodeService;
        _p2pNode = p2pNode;
        _peerManager = peerManager;
        _consensus = consensus;
    }

    /// <summary>
    /// Ping endpoint for heartbeat / peer discovery
    /// </summary>
    [HttpGet("ping")]
    public IActionResult Ping()
    {
        return Ok(_p2pNode.NodeId);
    }

    /// <summary>
    /// Receive transaction from peer
    /// </summary>
    [HttpPost("transaction")]
    public IActionResult ReceiveTransaction([FromBody] P2PMessage message)
    {
        try
        {
            var txMessage = JsonConvert.DeserializeObject<TransactionMessage>(message.Payload);
            if (txMessage?.Transaction != null)
            {
                // Add transaction to our pending pool
                _nodeService.AddTransaction(txMessage.Transaction);

                // Update peer's last seen
                _peerManager.UpdatePeerLastSeen(message.SenderId);

                Console.WriteLine($"[API] Received transaction from {message.SenderId}");
                return Ok();
            }
            return BadRequest("Invalid transaction");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] Error receiving transaction: {ex.Message}");
            return StatusCode(500, ex.Message);
        }
    }

    /// <summary>
    /// Receive newly mined block from peer
    /// </summary>
    [HttpPost("block")]
    public IActionResult ReceiveBlock([FromBody] P2PMessage message)
    {
        try
        {
            var blockMessage = JsonConvert.DeserializeObject<BlockMessage>(message.Payload);
            if (blockMessage?.Block != null)
            {
                var latestBlock = _nodeService.GetLatestBlock();

                // Validate the received block
                if (_consensus.ValidateReceivedBlock(blockMessage.Block, latestBlock))
                {
                    // Add block to our chain
                    // Note: You'll need to add an AddBlock method to BlockchainNodeService
                    Console.WriteLine($"[API] Received valid block from {message.SenderId}: {blockMessage.Block.Hash}");

                    // Trigger sync to ensure we have the full chain
                    _ = Task.Run(async () => await _consensus.SyncWithNetwork());
                }
                else
                {
                    Console.WriteLine($"[API] Received invalid block from {message.SenderId}");
                    // Trigger sync to resolve conflicts
                    _ = Task.Run(async () => await _consensus.SyncWithNetwork());
                }

                // Update peer's last seen
                _peerManager.UpdatePeerLastSeen(message.SenderId);

                return Ok();
            }
            return BadRequest("Invalid block");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] Error receiving block: {ex.Message}");
            return StatusCode(500, ex.Message);
        }
    }

    /// <summary>
    /// Send our blockchain to requesting peer
    /// </summary>
    [HttpGet("chain")]
    public IActionResult GetChain()
    {
        var chain = _nodeService.GetChain();
        var chainMessage = new ChainMessage
        {
            Chain = chain.Chain
        };
        return Ok(chainMessage);
    }

    /// <summary>
    /// Receive peer discovery request
    /// </summary>
    [HttpPost("peers")]
    public IActionResult ExchangePeers([FromBody] P2PMessage message)
    {
        try
        {
            var discoveryMessage = JsonConvert.DeserializeObject<PeerDiscoveryMessage>(message.Payload);
            if (discoveryMessage != null)
            {
                // Add the sender as a peer
                _peerManager.AddPeer(Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                                     message.SenderId);

                // Add any peers they know about
                // (In production, you'd validate these)

                Console.WriteLine($"[API] Peer discovery from {message.SenderId}");
                return Ok();
            }
            return BadRequest("Invalid peer discovery message");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] Error in peer exchange: {ex.Message}");
            return StatusCode(500, ex.Message);
        }
    }
}
```

### 1.3 Create BlockchainApiController.cs (User Interaction)

**File:** `Controllers/BlockchainApiController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using Models;
using Network;
using Services;

namespace Controllers;

[ApiController]
[Route("api/blockchain")]
public class BlockchainApiController : ControllerBase
{
    private readonly BlockchainNodeService _nodeService;
    private readonly P2PNode _p2pNode;
    private readonly PeerManager _peerManager;
    private readonly ConsensusManager _consensus;

    public BlockchainApiController(
        BlockchainNodeService nodeService,
        P2PNode p2pNode,
        PeerManager peerManager,
        ConsensusManager consensus)
    {
        _nodeService = nodeService;
        _p2pNode = p2pNode;
        _peerManager = peerManager;
        _consensus = consensus;
    }

    /// <summary>
    /// Get the entire blockchain
    /// </summary>
    [HttpGet("chain")]
    public IActionResult GetChain()
    {
        var chain = _nodeService.GetChain();
        return Ok(chain);
    }

    /// <summary>
    /// Get blockchain status
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var status = _nodeService.GetStatus();
        var peerCount = _peerManager.GetConnectedPeers().Count;

        return Ok(new
        {
            NodeId = _p2pNode.NodeId,
            Status = status,
            PeerCount = peerCount
        });
    }

    /// <summary>
    /// Validate blockchain
    /// </summary>
    [HttpGet("validate")]
    public IActionResult ValidateChain()
    {
        var isValid = _nodeService.IsChainValid();
        return Ok(new { IsValid = isValid });
    }

    /// <summary>
    /// Get balance for an address
    /// </summary>
    [HttpGet("balance/{address}")]
    public IActionResult GetBalance(string address)
    {
        var balance = _nodeService.GetBalance(address);
        return Ok(new { Address = address, Balance = balance });
    }

    /// <summary>
    /// Get list of connected peers
    /// </summary>
    [HttpGet("peers")]
    public IActionResult GetPeers()
    {
        var peers = _peerManager.GetAllPeers();
        return Ok(peers);
    }

    /// <summary>
    /// Get pending transactions
    /// </summary>
    [HttpGet("pending")]
    public IActionResult GetPendingTransactions()
    {
        var pending = _nodeService.GetPendingTransactions();
        return Ok(pending);
    }

    /// <summary>
    /// Submit a new transaction
    /// </summary>
    [HttpPost("transaction")]
    public async Task<IActionResult> AddTransaction([FromBody] TransactionRequest request)
    {
        try
        {
            var transaction = new Transaction(request.Sender, request.Recipient, request.Amount);

            // Add to local pending pool
            _nodeService.AddTransaction(transaction);

            // Broadcast to network
            await _p2pNode.BroadcastTransaction(transaction);

            var peerCount = _peerManager.GetConnectedPeers().Count;

            return Ok(new
            {
                Message = "Transaction added and broadcasted",
                TransactionId = transaction.TransactionId,
                PeerCount = peerCount
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Mine pending transactions
    /// </summary>
    [HttpPost("mine")]
    public async Task<IActionResult> MinePendingTransactions([FromBody] MineRequest request)
    {
        try
        {
            // Mine the block
            var block = _nodeService.MinePendingTransactions(request.MinerAddress);

            // Broadcast the new block to network
            await _p2pNode.BroadcastBlock(block);

            var peerCount = _peerManager.GetConnectedPeers().Count;

            return Ok(new
            {
                Message = "Block mined and broadcasted",
                Block = block,
                PeerCount = peerCount
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Manually trigger sync with network
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> SyncWithNetwork()
    {
        try
        {
            var replaced = await _consensus.ResolveConflicts();
            return Ok(new
            {
                Message = replaced ? "Chain replaced with longer valid chain" : "Our chain is the longest",
                ChainReplaced = replaced
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Connect to a new peer
    /// </summary>
    [HttpPost("peers/connect")]
    public async Task<IActionResult> ConnectToPeer([FromBody] ConnectPeerRequest request)
    {
        try
        {
            var connected = await _p2pNode.ConnectToPeer(request.Address);
            if (connected)
            {
                return Ok(new { Message = $"Connected to peer at {request.Address}" });
            }
            return BadRequest(new { Error = $"Failed to connect to {request.Address}" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message });
        }
    }
}

// Request models
public class TransactionRequest
{
    public string Sender { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public double Amount { get; set; }
}

public class MineRequest
{
    public string MinerAddress { get; set; } = string.Empty;
}

public class ConnectPeerRequest
{
    public string Address { get; set; } = string.Empty;
}
```

## Step 2: Configure Web Host

### 2.1 Replace Program.cs

**File:** `Program.cs`

```csharp
using Network;
using Services;
using Storage;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddNewtonsoftJson(); // Support for Newtonsoft.Json

// CORS configuration (allow all origins for development)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register blockchain services as singletons
builder.Services.AddSingleton<BlockchainStorage>(sp =>
{
    var dataDir = builder.Configuration["Blockchain:DataDirectory"] ?? "/app/data";
    return new BlockchainStorage(dataDir);
});

builder.Services.AddSingleton<BlockchainNodeService>(sp =>
{
    var storage = sp.GetRequiredService<BlockchainStorage>();
    var difficulty = builder.Configuration.GetValue<int>("Blockchain:Difficulty", 2);
    var miningReward = builder.Configuration.GetValue<double>("Blockchain:MiningReward", 50.0);
    return new BlockchainNodeService(storage, difficulty, miningReward);
});

builder.Services.AddSingleton<PeerManager>(sp =>
{
    var dataDir = builder.Configuration["Blockchain:DataDirectory"] ?? "/app/data";
    var maxPeers = builder.Configuration.GetValue<int>("P2P:MaxPeers", 10);
    return new PeerManager(dataDir, maxPeers);
});

builder.Services.AddSingleton<P2PNode>(sp =>
{
    var peerManager = sp.GetRequiredService<PeerManager>();
    var heartbeatInterval = builder.Configuration.GetValue<int>("P2P:HeartbeatInterval", 30000);
    return new P2PNode(peerManager, heartbeatInterval);
});

builder.Services.AddSingleton<ConsensusManager>(sp =>
{
    var nodeService = sp.GetRequiredService<BlockchainNodeService>();
    var p2pNode = sp.GetRequiredService<P2PNode>();
    var peerManager = sp.GetRequiredService<PeerManager>();
    return new ConsensusManager(nodeService, p2pNode, peerManager);
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseCors("AllowAll");
app.UseRouting();
app.MapControllers();

// Start P2P node
var p2pNode = app.Services.GetRequiredService<P2PNode>();
p2pNode.Start();

// Connect to seed nodes from configuration
var seedNodes = builder.Configuration.GetSection("P2P:SeedNodes").Get<string[]>();
if (seedNodes != null && seedNodes.Length > 0)
{
    Console.WriteLine($"Connecting to {seedNodes.Length} seed nodes...");
    foreach (var seedNode in seedNodes)
    {
        _ = Task.Run(async () => await p2pNode.ConnectToPeer(seedNode));
    }
}

// Start periodic sync
var consensus = app.Services.GetRequiredService<ConsensusManager>();
var syncInterval = builder.Configuration.GetValue<int>("P2P:SyncInterval", 60000);
var syncTimer = new System.Threading.Timer(async _ =>
{
    await consensus.SyncWithNetwork();
}, null, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(syncInterval));

Console.WriteLine($"=== Blockchain Node Started ===");
Console.WriteLine($"Node ID: {p2pNode.NodeId}");
Console.WriteLine($"Listening on: {builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5000"}");

app.Run();
```

## Step 3: Create Configuration File

### 3.1 Create appsettings.json

**File:** `appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Blockchain": {
    "Difficulty": 2,
    "MiningReward": 50.0,
    "DataDirectory": "/app/data"
  },
  "P2P": {
    "ListenPort": 5001,
    "SeedNodes": [],
    "MaxPeers": 10,
    "HeartbeatInterval": 30000,
    "SyncInterval": 60000
  }
}
```

### 3.2 Create appsettings.Development.json

**File:** `appsettings.Development.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  },
  "Blockchain": {
    "Difficulty": 1,
    "DataDirectory": "./data"
  },
  "P2P": {
    "ListenPort": 5001,
    "SeedNodes": []
  }
}
```

## Step 4: Add Required NuGet Package

Update `blockchain-example-project.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>blockchain_example_project</RootNamespace>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.NewtonsoftJson" Version="8.0.0" />
  </ItemGroup>
</Project>
```

Restore packages:
```bash
dotnet restore
```

## Step 5: Testing

### 5.1 Run the Application

```bash
dotnet run
```

**Expected Output:**
```
[Storage] Loaded existing blockchain with X blocks
[PeerManager] Loaded X peers from file
[P2P] Node initialized with ID: xxxx-xxxx-xxxx
[P2P] Node started
=== Blockchain Node Started ===
Node ID: xxxx-xxxx-xxxx-xxxx
Listening on: http://localhost:5000
```

### 5.2 Test API Endpoints with curl

**Get Status:**
```bash
curl http://localhost:5000/api/blockchain/status
```

**Get Chain:**
```bash
curl http://localhost:5000/api/blockchain/chain
```

**Add Transaction:**
```bash
curl -X POST http://localhost:5000/api/blockchain/transaction \
  -H "Content-Type: application/json" \
  -d '{"sender":"Alice","recipient":"Bob","amount":50}'
```

**Mine Block:**
```bash
curl -X POST http://localhost:5000/api/blockchain/mine \
  -H "Content-Type: application/json" \
  -d '{"minerAddress":"Miner1"}'
```

**Get Balance:**
```bash
curl http://localhost:5000/api/blockchain/balance/Miner1
```

**Get Peers:**
```bash
curl http://localhost:5000/api/blockchain/peers
```

**Connect to Peer:**
```bash
curl -X POST http://localhost:5000/api/blockchain/peers/connect \
  -H "Content-Type: application/json" \
  -d '{"address":"localhost:5002"}'
```

### 5.3 Test P2P Communication

Run two instances on different ports:

**Terminal 1:**
```bash
ASPNETCORE_URLS="http://localhost:5001" dotnet run
```

**Terminal 2:**
```bash
ASPNETCORE_URLS="http://localhost:5002" dotnet run
```

**Connect them:**
```bash
# From Node 1, connect to Node 2
curl -X POST http://localhost:5001/api/blockchain/peers/connect \
  -H "Content-Type: application/json" \
  -d '{"address":"localhost:5002"}'
```

**Add transaction on Node 1:**
```bash
curl -X POST http://localhost:5001/api/blockchain/transaction \
  -H "Content-Type: application/json" \
  -d '{"sender":"Alice","recipient":"Bob","amount":100}'
```

**Check pending transactions on Node 2:**
```bash
curl http://localhost:5002/api/blockchain/pending
```

(Should show the transaction after a moment)

## Completion Checklist

- [ ] NodeApiController.cs created (P2P endpoints)
- [ ] BlockchainApiController.cs created (User endpoints)
- [ ] Program.cs updated with web host configuration
- [ ] appsettings.json created
- [ ] appsettings.Development.json created
- [ ] CORS configured
- [ ] Services registered in DI container
- [ ] P2P node starts automatically
- [ ] Seed nodes connection attempted on startup
- [ ] Periodic sync timer configured
- [ ] All endpoints tested with curl
- [ ] Two-node communication tested

## Troubleshooting

### Issue: Port already in use

**Solution:** Change port in ASPNETCORE_URLS
```bash
ASPNETCORE_URLS="http://localhost:5002" dotnet run
```

### Issue: CORS errors

**Solution:** Verify CORS policy is applied before UseRouting

### Issue: Endpoints return 404

**Solution:** Ensure MapControllers() is called

### Issue: Transactions not broadcasting

**Solution:** Check that peers are connected via `/api/blockchain/peers`

## Next Steps

Once this phase is complete:
1. Test all API endpoints
2. Verify P2P communication between nodes
3. Move on to **Phase 4: Blazor Web UI** for a user-friendly interface
4. The API is now fully functional and ready for the frontend!

## How to Start/Stop/Interact

**Start:**
```bash
dotnet run
# or with custom port
ASPNETCORE_URLS="http://localhost:5001" dotnet run
```

**Stop:**
```
Ctrl+C
```

**Interact:**
- Use curl (see examples above)
- Use Postman or any HTTP client
- Web UI (coming in Phase 4)
- Direct API calls from code

**View Logs:**
Console shows all activity in real-time

## API Documentation Summary

### P2P Endpoints (Node-to-Node)
- `GET /api/node/ping` - Heartbeat check
- `POST /api/node/transaction` - Receive transaction from peer
- `POST /api/node/block` - Receive block from peer
- `GET /api/node/chain` - Send chain to peer
- `POST /api/node/peers` - Peer discovery

### User Endpoints (Local Interaction)
- `GET /api/blockchain/chain` - View blockchain
- `GET /api/blockchain/status` - Node status
- `GET /api/blockchain/validate` - Validate chain
- `GET /api/blockchain/balance/{address}` - Get balance
- `GET /api/blockchain/peers` - List peers
- `GET /api/blockchain/pending` - Pending transactions
- `POST /api/blockchain/transaction` - Submit transaction
- `POST /api/blockchain/mine` - Mine block
- `POST /api/blockchain/sync` - Sync with network
- `POST /api/blockchain/peers/connect` - Connect to peer

## Summary

You now have:
- Complete REST API for blockchain operations
- P2P communication endpoints
- User interaction endpoints
- Automatic peer connection on startup
- Periodic blockchain synchronization
- CORS enabled for web frontends
- Configuration-based setup
- Multi-node capability

Your blockchain node is now a fully functional web service!
