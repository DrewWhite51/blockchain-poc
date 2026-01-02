# Architecture Overview

> **📚 Understanding Design Decisions:** This document explains *how* the system works. For explanations of *why* these architectural choices were made and what problems they solve, see [WHY-THESE-FEATURES.md](WHY-THESE-FEATURES.md).

## System Design

This blockchain implementation follows a **decentralized peer-to-peer (P2P) network architecture**, similar to Bitcoin and Ethereum. Each node operates independently with its own copy of the blockchain, synchronized through a consensus protocol.

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         User Browser                             │
│                      (Blazor Web UI)                             │
└────────────────────────────┬────────────────────────────────────┘
                             │ HTTP
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Blockchain Node (Docker Container)            │
│                                                                   │
│  ┌──────────────────┐    ┌──────────────────┐                   │
│  │   Web UI Layer   │    │   API Layer      │                   │
│  │  (Blazor Pages)  │    │  (Controllers)   │                   │
│  └────────┬─────────┘    └────────┬─────────┘                   │
│           │                       │                              │
│           └───────────┬───────────┘                              │
│                       ▼                                          │
│           ┌─────────────────────┐                                │
│           │  Service Layer      │                                │
│           │  BlockchainNode     │◄──── Thread Safety (RWLock)    │
│           │  Service            │                                │
│           └──────┬──────────────┘                                │
│                  │                                               │
│      ┌───────────┼───────────┐                                   │
│      ▼           ▼           ▼                                   │
│  ┌────────┐ ┌────────┐ ┌──────────┐                             │
│  │ P2P    │ │Storage │ │Consensus │                             │
│  │ Node   │ │ Layer  │ │ Manager  │                             │
│  └───┬────┘ └───┬────┘ └────┬─────┘                             │
│      │          │           │                                    │
│      │      ┌───▼───────────▼───┐                                │
│      │      │  blockchain.json  │                                │
│      │      │  (File Storage)   │                                │
│      │      └───────────────────┘                                │
└──────┼────────────────────────────────────────────────────────────┘
       │ P2P Protocol
       │ (HTTP/JSON)
       │
       ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Other Blockchain Nodes                        │
│                  (Peer-to-Peer Network)                          │
└─────────────────────────────────────────────────────────────────┘
```

## Core Components

### 1. Models Layer

**Location**: `Models/`

**Components**:
- `Block.cs`: Individual block structure
- `Transaction.cs`: Transaction data model
- `Blockchain.cs`: Chain management and validation

**Responsibilities**:
- Define data structures
- Implement proof-of-work mining
- Calculate cryptographic hashes (SHA-256)
- Validate chain integrity

**Key Methods**:
```
Block.MineBlock() → Proof-of-Work algorithm
Block.CalculateHash() → SHA-256 hash computation
Blockchain.IsChainValid() → Chain integrity verification
Blockchain.GetBalance() → Calculate address balance
```

### 2. Storage Layer

**Location**: `Storage/BlockchainStorage.cs`

**Responsibilities**:
- Persist blockchain to JSON file
- Load blockchain on startup
- Atomic file writes (prevent corruption)
- Backup management (keep last 5)

**Data Flow**:
```
BlockChain Object
    ↓ Serialize (Newtonsoft.Json)
blockchain.json.tmp
    ↓ Atomic Move
blockchain.json
    ↓ Backup
backups/blockchain_20240115_103000.json
```

**Thread Safety**: File I/O protected by service-layer locks

### 3. Network Layer

**Location**: `Network/`

**Components**:

#### P2PNode.cs
- Manages peer connections
- Broadcasts transactions and blocks
- Handles incoming P2P messages

#### PeerManager.cs
- Tracks known peers
- Monitors peer health (heartbeat)
- Maintains connection status
- Prunes dead peers

#### ConsensusManager.cs
- Implements longest chain rule
- Synchronizes blockchain with network
- Resolves chain conflicts

#### MessageTypes.cs
- Defines P2P message protocol
- Message types: NewTransaction, NewBlock, RequestChain, SendChain, Ping

**P2P Communication Flow**:
```
Node A                          Node B                          Node C
  │                               │                               │
  │──── Mine Block ────────────►  │                               │
  │                               │──── Broadcast Block ────────► │
  │                               │                               │
  │                               │◄──── Validate Block ──────────│
  │                               │                               │
  │◄──── Add to Chain ───────────│                               │
```

### 4. Service Layer

**Location**: `Services/BlockchainNodeService.cs`

**Responsibilities**:
- Thread-safe blockchain operations
- Orchestrate storage, network, consensus
- Expose business logic to API layer

**Thread Safety Design**:
```csharp
ReaderWriterLockSlim:
  - Read Lock: Multiple threads can read simultaneously
    └─ GetChain(), GetBalance(), IsChainValid()

  - Write Lock: Exclusive access for mutations
    └─ AddTransaction(), MinePendingTransactions()
```

**Concurrency Scenarios**:
1. **Web UI reads** blockchain while **P2P receives** new block
2. **User mines** block while **peer broadcasts** transaction
3. **Multiple API calls** read balance simultaneously

### 5. API Layer

**Location**: `Controllers/`

**Controllers**:

#### NodeApiController.cs (P2P Endpoints)
- `POST /api/node/transaction` - Receive transaction from peer
- `POST /api/node/block` - Receive block from peer
- `GET /api/node/chain` - Send chain to peer
- `GET /api/node/ping` - Heartbeat check

#### BlockchainApiController.cs (User Endpoints)
- `GET /api/blockchain/status` - Node status
- `GET /api/blockchain/chain` - Full blockchain
- `POST /api/blockchain/transaction` - Submit transaction
- `POST /api/blockchain/mine` - Mine block
- `GET /api/blockchain/balance/{address}` - Check balance
- `GET /api/blockchain/peers` - List peers
- `POST /api/blockchain/peers/connect` - Connect to peer

### 6. UI Layer

**Location**: `Pages/`

**Components**:
- `_Host.cshtml`: Blazor Server entry point
- `App.razor`: Router configuration
- `Shared/MainLayout.razor`: Layout template
- `Index.razor`: Main dashboard

**Features**:
- Real-time blockchain viewer
- Transaction submission form
- Mining interface
- Balance checker
- Peer management
- Auto-refresh (3-second interval)

## Data Flow Diagrams

### Transaction Flow

```
User Browser
    │
    │ 1. Submit Transaction
    ▼
Blazor UI (Index.razor)
    │
    │ 2. Create Transaction object
    ▼
BlockchainNodeService
    │
    ├─► 3a. Add to pending pool
    │   (Write Lock)
    │
    ├─► 3b. Save to file
    │   (BlockchainStorage)
    │
    └─► 3c. Broadcast to peers
        (P2PNode)
        │
        ▼
Peer Nodes
    │
    │ 4. Receive transaction
    ▼
Add to pending pool
```

### Mining Flow

```
User clicks "Mine Block"
    │
    │ 1. Enter miner address
    ▼
BlockchainNodeService.MinePendingTransactions()
    │
    │ 2. Acquire Write Lock
    ▼
Create new block with:
  - Pending transactions
  - Previous block hash
  - Timestamp
    │
    │ 3. Proof-of-Work (CPU intensive)
    ▼
Block.MineBlock(difficulty)
  └─► Find nonce where Hash starts with "00..."
    │
    │ 4. Add mining reward transaction
    ▼
Blockchain.AddBlock()
    │
    ├─► 5a. Validate block
    │
    ├─► 5b. Append to chain
    │
    ├─► 5c. Clear pending pool
    │
    └─► 5d. Save to file
        │
        │ 6. Broadcast block to peers
        ▼
P2PNode.BroadcastBlock()
```

### Consensus Synchronization Flow

```
Node A (Chain Length: 5)              Node B (Chain Length: 7)
    │                                        │
    │──── 1. Heartbeat Request ─────────────►│
    │                                        │
    │◄─── 2. Response: Chain Length 7 ──────│
    │                                        │
    │ 3. Detect longer chain                 │
    │                                        │
    │──── 4. Request Full Chain ────────────►│
    │                                        │
    │◄─── 5. Send Full Blockchain ──────────│
    │                                        │
    │ 6. Validate received chain             │
    │    (IsChainValid())                    │
    │                                        │
    │ 7. Replace local chain if valid        │
    │    (Consensus: Longest Chain Rule)     │
    │                                        │
    │ 8. Save to file                        │
    │                                        │
    ▼                                        ▼
Updated Chain (Length: 7)             Chain (Length: 7)
```

## Network Topology

### Seed Node Discovery

```
New Node
    │
    │ 1. Start with seed node addresses
    │    (from docker-compose or environment)
    ▼
Connect to Seed Nodes
    │
    │ 2. Exchange peer lists
    ▼
Discover Network Peers
    │
    │ 3. Connect to additional peers
    ▼
Fully Connected to Network
```

### Peer-to-Peer Mesh Network

```
        Node A ◄──────► Node B
          │  ╲           │  ╱
          │    ╲         │ ╱
          │      ╲       │╱
          │        ╲     ╱│
          │          ╲ ╱  │
          │           ╳   │
          │         ╱  ╲  │
          │       ╱      ╲│
          │     ╱         │╲
          │   ╱           │  ╲
          ▼ ╱             ▼    ╲
        Node C ◄──────► Node D
```

**Characteristics**:
- Decentralized (no master node)
- Resilient (node failures don't break network)
- Gossip protocol for message propagation
- Eventual consistency

## Consensus Mechanism

### Longest Chain Rule

**Algorithm**:
```
When receiving a block from peer:
  1. Validate block structure
  2. Verify proof-of-work
  3. Check transactions are valid
  4. If valid, add to local chain
  5. Broadcast to other peers

When detecting longer chain:
  1. Request full chain from peer
  2. Validate entire chain
  3. If valid and longer, replace local chain
  4. Persist to disk
  5. Continue mining on new chain
```

**Conflict Resolution**:
- Chain with most cumulative proof-of-work wins
- In this implementation: longest chain = most blocks
- Orphaned blocks are discarded

## Threading and Concurrency

### Thread Safety Strategy

**ReaderWriterLockSlim Pattern**:

```csharp
// Multiple readers allowed
public BlockChain GetChain()
{
    _lock.EnterReadLock();
    try
    {
        return _blockchain; // Thread-safe read
    }
    finally
    {
        _lock.ExitReadLock();
    }
}

// Exclusive writer
public void AddTransaction(Transaction tx)
{
    _lock.EnterWriteLock();
    try
    {
        _blockchain.AddTransaction(tx); // Exclusive write
        _storage.SaveToFile(_blockchain);
    }
    finally
    {
        _lock.ExitWriteLock();
    }
}
```

**Concurrent Scenarios Handled**:
1. UI reads while peer broadcasts transaction ✓
2. Multiple API balance checks ✓
3. Mining while syncing with network ✓
4. File save during read operations ✓

## Storage Architecture

### File Structure

```
/app/data/
├── blockchain.json           # Main blockchain file
└── backups/
    ├── blockchain_20240115_100000.json
    ├── blockchain_20240115_103000.json
    ├── blockchain_20240115_110000.json
    ├── blockchain_20240115_113000.json
    └── blockchain_20240115_120000.json
```

### blockchain.json Structure

```json
{
  "Chain": [
    {
      "Index": 0,
      "Timestamp": "2024-01-15T10:00:00Z",
      "Transactions": [],
      "PreviousHash": "0",
      "Hash": "genesis...",
      "Nonce": 0
    },
    {
      "Index": 1,
      "Timestamp": "2024-01-15T10:05:00Z",
      "Transactions": [
        {
          "TransactionId": "tx123...",
          "Sender": "Alice",
          "Recipient": "Bob",
          "Amount": 50.0
        }
      ],
      "PreviousHash": "genesis...",
      "Hash": "0000abc...",
      "Nonce": 45678
    }
  ],
  "Difficulty": 2,
  "MiningReward": 50.0,
  "PendingTransactions": []
}
```

### Atomic Write Pattern

```
1. Serialize blockchain to JSON
2. Write to blockchain.json.tmp
3. Backup existing blockchain.json
4. Move .tmp to blockchain.json (atomic)
5. Cleanup old backups (keep 5)
```

## Configuration

### appsettings.json Structure

```json
{
  "Blockchain": {
    "Difficulty": 2,
    "MiningReward": 50.0,
    "DataDirectory": "/app/data"
  },
  "P2P": {
    "ListenPort": 5001,
    "SeedNodes": [
      "blockchain-node-1:5001"
    ],
    "MaxPeers": 10,
    "HeartbeatInterval": 30000,
    "SyncInterval": 60000
  }
}
```

### Environment Variables (Docker)

```bash
Blockchain__Difficulty=2
Blockchain__MiningReward=50.0
Blockchain__DataDirectory=/app/data
P2P__ListenPort=5001
P2P__SeedNodes__0=node1:5001
P2P__SeedNodes__1=node2:5001
P2P__MaxPeers=10
P2P__HeartbeatInterval=30000
P2P__SyncInterval=60000
```

## Docker Architecture

### Container Structure

```
┌─────────────────────────────────────┐
│   Docker Container                  │
│                                     │
│   ┌─────────────────────────────┐   │
│   │  ASP.NET Core Runtime       │   │
│   │  (Port 8080 - HTTP)         │   │
│   └─────────────────────────────┘   │
│                                     │
│   ┌─────────────────────────────┐   │
│   │  P2P Listener               │   │
│   │  (Port 5001 - P2P)          │   │
│   └─────────────────────────────┘   │
│                                     │
│   ┌─────────────────────────────┐   │
│   │  Volume Mount               │   │
│   │  /app/data → blockchain.json│   │
│   └─────────────────────────────┘   │
│                                     │
└─────────────────────────────────────┘
```

### Multi-Node Network (Docker Compose)

```
┌──────────────────────┐    ┌──────────────────────┐    ┌──────────────────────┐
│  blockchain-node-1   │    │  blockchain-node-2   │    │  blockchain-node-3   │
│                      │    │                      │    │                      │
│  HTTP: 8081          │    │  HTTP: 8082          │    │  HTTP: 8083          │
│  P2P:  5001          │◄──►│  P2P:  5001          │◄──►│  P2P:  5001          │
│                      │    │                      │    │                      │
│  Volume: node-1-data │    │  Volume: node-2-data │    │  Volume: node-3-data │
└──────────────────────┘    └──────────────────────┘    └──────────────────────┘
         │                           │                           │
         └───────────────────────────┴───────────────────────────┘
                        Shared Docker Network
                       (blockchain-network)
```

## Security Considerations

### Current Implementation (PoC)

- ✗ No authentication on API endpoints
- ✗ No TLS/HTTPS encryption
- ✗ No transaction signatures
- ✗ No peer authentication
- ✗ No rate limiting

### Production Recommendations

```
Authentication Layer
    │
    ├─► API Key Authentication
    ├─► JWT Tokens for UI
    └─► mTLS for P2P communication

Transaction Security
    │
    ├─► Digital Signatures (ECDSA)
    ├─► Public/Private Key Pairs
    └─► Transaction Validation

Network Security
    │
    ├─► HTTPS/TLS 1.3
    ├─► Peer Reputation System
    ├─► Rate Limiting (DDoS protection)
    └─► Firewall Rules
```

## Performance Characteristics

### Mining Performance

```
Difficulty 1: ~10-100 ms
Difficulty 2: ~100-1000 ms
Difficulty 3: ~1-10 seconds
Difficulty 4: ~10-60 seconds
```

**Factors**:
- CPU speed
- Number of transactions in block
- Random nonce discovery

### Network Latency

```
Transaction Broadcast: ~10-100 ms per peer
Block Broadcast: ~100-500 ms per peer
Chain Sync: ~1-5 seconds (depends on chain length)
Heartbeat: 30 seconds (configurable)
Sync Interval: 60 seconds (configurable)
```

### Storage Growth

```
Block Size: ~1-5 KB (depends on transactions)
100 blocks: ~100-500 KB
1,000 blocks: ~1-5 MB
10,000 blocks: ~10-50 MB
```

## Scalability Considerations

### Current Limitations

- Single-threaded mining (CPU-bound)
- In-memory blockchain (limited by RAM)
- File-based storage (not optimized for large chains)
- No transaction batching
- No block pruning

### Improvement Strategies

1. **Parallel Mining**: Multi-threaded proof-of-work
2. **Database Storage**: Move to embedded DB (SQLite, LevelDB)
3. **Merkle Trees**: Efficient transaction verification
4. **Block Pruning**: Remove old transaction data
5. **State Database**: Separate chain state from full history
6. **Transaction Pool**: Priority queues, fee markets

## Deployment Patterns

### Single Node (Development)

```bash
dotnet run
# Access: http://localhost:5000
```

### Multi-Node Local Network

```bash
docker-compose up -d
# Node 1: http://localhost:8081
# Node 2: http://localhost:8082
# Node 3: http://localhost:8083
```

### Cloud Deployment

```
Load Balancer (Optional - for UI only)
    │
    ├─► Node A (US-East)
    ├─► Node B (EU-West)
    └─► Node C (Asia-Pacific)
         │
         └─── P2P Mesh Network
```

**Cloud Options**:
- AWS ECS/Fargate
- Google Cloud Run
- Azure Container Instances
- DigitalOcean App Platform
- Generic VPS (Docker Compose)

## Comparison to Real Blockchains

### Similarities to Bitcoin/Ethereum

✓ Proof-of-Work consensus
✓ Decentralized P2P network
✓ Longest chain rule
✓ Transaction broadcasting
✓ Mining rewards
✓ Hash-based chain linking

### Differences (Simplifications)

✗ No Merkle trees
✗ No UTXO model (account-based)
✗ No transaction signatures
✗ No script/smart contracts
✗ Simple difficulty (fixed)
✗ No difficulty adjustment
✗ No mempool prioritization
✗ No SPV (Simplified Payment Verification)
✗ No segwit/witness data

## Testing Strategy

### Unit Testing Targets

```
Models/
  └─ Block.CalculateHash()
  └─ Block.MineBlock()
  └─ Blockchain.IsChainValid()
  └─ Blockchain.GetBalance()

Storage/
  └─ BlockchainStorage.SaveToFile()
  └─ BlockchainStorage.LoadFromFile()

Network/
  └─ PeerManager.AddPeer()
  └─ ConsensusManager.ReplaceChain()
```

### Integration Testing Scenarios

1. **Multi-Node Transaction**: Submit on Node A, verify on Node B
2. **Consensus**: Mine different blocks on 2 nodes, verify longest chain wins
3. **Peer Discovery**: Start 3 nodes, verify they discover each other
4. **Chain Sync**: Start node with empty chain, verify it syncs from network
5. **Node Restart**: Restart node, verify chain loads from disk

### Manual Testing Checklist

- [ ] Submit transaction through UI
- [ ] Mine block and see reward
- [ ] Check balance reflects transactions
- [ ] Connect two nodes manually
- [ ] Verify transaction broadcasts to peer
- [ ] Verify block broadcasts to peer
- [ ] Restart node, verify chain persists
- [ ] Start 3-node network, verify mesh forms

## Monitoring and Observability

### Key Metrics to Track

```
Node Health
  ├─ Chain length
  ├─ Chain validity (true/false)
  ├─ Pending transactions count
  └─ Last block timestamp

Peer Network
  ├─ Connected peer count
  ├─ Heartbeat failures
  ├─ Chain sync operations
  └─ Message broadcast latency

Performance
  ├─ Mining time per block
  ├─ Transaction throughput
  ├─ API response times
  └─ File I/O duration
```

### Logging Strategy

```
[Storage] Blockchain saved to /app/data/blockchain.json
[Node] Transaction added: tx123...
[Node] Mining block for Miner1...
[Node] Block mined! Hash: 0000abc...
[P2P] Broadcasting transaction to 3 peers
[P2P] Connected to peer: node-2:5001
[Consensus] Replacing chain with longer chain (5 → 7 blocks)
[PeerManager] Peer node-3:5001 disconnected (timeout)
```

## Future Enhancements

### Phase 7: Smart Contracts
- Virtual machine for contract execution
- Solidity-like contract language
- State storage per contract

### Phase 8: Wallet System
- Public/private key generation
- Transaction signing (ECDSA)
- Address derivation (BIP32/44)

### Phase 9: Advanced Features
- Merkle tree implementation
- SPV (light clients)
- Dynamic difficulty adjustment
- Transaction fees
- Mempool management

### Phase 10: Performance Optimization
- Database storage (PostgreSQL, RocksDB)
- Block pruning
- State snapshots
- Parallel transaction validation

## Troubleshooting Reference

### Common Issues

**Nodes can't connect**
- Check firewall allows port 5001
- Verify seed node addresses are correct
- Check Docker network configuration

**Blockchain not syncing**
- Trigger manual sync: `POST /api/blockchain/sync`
- Verify all nodes are running
- Check sync interval configuration

**Mining takes too long**
- Reduce difficulty in appsettings.json
- Use more CPU resources
- Verify proof-of-work algorithm

**Data lost after restart**
- Ensure Docker volume is mounted
- Check volume exists: `docker volume ls`
- Verify data directory permissions

## Conclusion

This architecture implements a functional decentralized blockchain network with:

✓ Peer-to-peer communication
✓ Consensus through longest chain rule
✓ Proof-of-Work mining
✓ File-based persistence
✓ Thread-safe concurrent operations
✓ Web-based user interface
✓ Docker containerization
✓ Multi-node networking

It serves as an educational proof-of-concept demonstrating core blockchain principles while remaining simple enough to understand and extend.
