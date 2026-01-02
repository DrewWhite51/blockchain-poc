# Architecture Diagrams (Mermaid)

## System Architecture Overview

```mermaid
graph TB
    subgraph "User Layer"
        Browser[User Browser<br/>Blazor Web UI]
    end

    subgraph "Docker Container - Blockchain Node"
        subgraph "Presentation Layer"
            UI[Web UI Layer<br/>Blazor Pages]
            API[API Layer<br/>Controllers]
        end

        subgraph "Business Layer"
            Service[Service Layer<br/>BlockchainNodeService<br/>Thread Safety: RWLock]
        end

        subgraph "Infrastructure Layer"
            P2P[P2P Node<br/>Network Manager]
            Storage[Storage Layer<br/>BlockchainStorage]
            Consensus[Consensus Manager<br/>Longest Chain]
        end

        subgraph "Data Layer"
            File[(blockchain.json<br/>File Storage)]
        end
    end

    subgraph "P2P Network"
        Peers[Other Blockchain Nodes<br/>Peer-to-Peer Network]
    end

    Browser -->|HTTP| UI
    Browser -->|HTTP| API
    UI --> Service
    API --> Service
    Service --> P2P
    Service --> Storage
    Service --> Consensus
    Storage --> File
    Consensus --> File
    P2P <-->|P2P Protocol<br/>HTTP/JSON| Peers

    style Browser fill:#e1f5ff
    style Service fill:#fff3cd
    style P2P fill:#d4edda
    style Storage fill:#d4edda
    style Consensus fill:#d4edda
    style File fill:#f8d7da
    style Peers fill:#e1f5ff
```

## Component Architecture

```mermaid
graph LR
    subgraph "Models Layer"
        Block[Block.cs<br/>- Index<br/>- Hash<br/>- Transactions<br/>- MineBlock]
        Transaction[Transaction.cs<br/>- Sender<br/>- Recipient<br/>- Amount]
        Blockchain[Blockchain.cs<br/>- Chain<br/>- Pending Tx<br/>- IsChainValid]
    end

    subgraph "Storage Layer"
        BCS[BlockchainStorage.cs<br/>- SaveToFile<br/>- LoadFromFile<br/>- Atomic Writes]
    end

    subgraph "Network Layer"
        P2PNode[P2PNode.cs<br/>- BroadcastTx<br/>- BroadcastBlock<br/>- ConnectToPeer]
        PeerMgr[PeerManager.cs<br/>- AddPeer<br/>- Heartbeat<br/>- GetPeers]
        ConsMgr[ConsensusManager.cs<br/>- ReplaceChain<br/>- SyncChain<br/>- LongestChain]
        MsgTypes[MessageTypes.cs<br/>- NewTransaction<br/>- NewBlock<br/>- RequestChain]
    end

    subgraph "Service Layer"
        BNS[BlockchainNodeService.cs<br/>- AddTransaction<br/>- MinePending<br/>- GetBalance<br/>- Thread-Safe]
    end

    subgraph "API Layer"
        NodeAPI[NodeApiController.cs<br/>P2P Endpoints]
        BCAPI[BlockchainApiController.cs<br/>User Endpoints]
    end

    subgraph "UI Layer"
        Index[Index.razor<br/>Dashboard]
        Layout[MainLayout.razor<br/>Navigation]
    end

    Blockchain --> Block
    Blockchain --> Transaction
    BCS --> Blockchain
    BNS --> Blockchain
    BNS --> BCS
    BNS --> P2PNode
    BNS --> ConsMgr
    P2PNode --> PeerMgr
    P2PNode --> MsgTypes
    ConsMgr --> PeerMgr
    NodeAPI --> BNS
    BCAPI --> BNS
    Index --> BNS
    Index --> P2PNode
    Layout --> PeerMgr
```

## Transaction Flow

```mermaid
sequenceDiagram
    participant User as User Browser
    participant UI as Blazor UI
    participant Service as BlockchainNodeService
    participant Storage as BlockchainStorage
    participant P2P as P2PNode
    participant Peers as Peer Nodes

    User->>UI: Submit Transaction
    UI->>UI: Create Transaction Object
    UI->>Service: AddTransaction(tx)

    activate Service
    Service->>Service: Acquire Write Lock
    Service->>Service: Add to Pending Pool
    Service->>Storage: SaveToFile()
    activate Storage
    Storage->>Storage: Serialize to JSON
    Storage->>Storage: Atomic Write
    Storage-->>Service: Success
    deactivate Storage

    Service->>P2P: BroadcastTransaction(tx)
    activate P2P
    P2P->>Peers: POST /api/node/transaction
    Peers-->>P2P: ACK
    deactivate P2P

    Service->>Service: Release Write Lock
    Service-->>UI: Transaction Added
    deactivate Service

    UI-->>User: Success Message

    Peers->>Peers: Add to Pending Pool
```

## Mining Flow

```mermaid
sequenceDiagram
    participant User as User Browser
    participant Service as BlockchainNodeService
    participant BC as Blockchain
    participant Block as Block
    participant Storage as BlockchainStorage
    participant P2P as P2PNode
    participant Peers as Peer Nodes

    User->>Service: Mine Block (minerAddress)

    activate Service
    Service->>Service: Acquire Write Lock
    Service->>BC: MinePendingTransactions(miner)

    activate BC
    BC->>Block: Create New Block
    activate Block
    Block->>Block: Add Pending Transactions
    Block->>Block: Set Previous Hash
    Block->>Block: MineBlock(difficulty)

    Note over Block: Proof-of-Work<br/>Find nonce where<br/>Hash starts with "00..."

    Block-->>BC: Mined Block
    deactivate Block

    BC->>BC: Add Mining Reward
    BC->>BC: Append Block to Chain
    BC->>BC: Clear Pending Pool
    BC-->>Service: Latest Block
    deactivate BC

    Service->>Storage: SaveToFile()
    activate Storage
    Storage->>Storage: Atomic Write
    Storage-->>Service: Success
    deactivate Storage

    Service->>P2P: BroadcastBlock(block)
    activate P2P
    P2P->>Peers: POST /api/node/block
    Peers-->>P2P: ACK
    deactivate P2P

    Service->>Service: Release Write Lock
    Service-->>User: Block Mined
    deactivate Service

    Peers->>Peers: Validate & Add Block
```

## Consensus Synchronization Flow

```mermaid
sequenceDiagram
    participant NodeA as Node A<br/>(Chain Length: 5)
    participant PeerMgrA as PeerManager A
    participant ConsensusA as Consensus A
    participant NodeB as Node B<br/>(Chain Length: 7)
    participant StorageA as Storage A

    Note over NodeA,NodeB: Periodic Heartbeat

    PeerMgrA->>NodeB: GET /api/node/ping
    NodeB-->>PeerMgrA: Chain Length: 7

    PeerMgrA->>PeerMgrA: Update Peer Info
    PeerMgrA->>ConsensusA: Notify: Peer has longer chain

    activate ConsensusA
    ConsensusA->>ConsensusA: Detect Longer Chain (5 < 7)
    ConsensusA->>NodeB: GET /api/node/chain
    NodeB-->>ConsensusA: Full Blockchain (7 blocks)

    ConsensusA->>ConsensusA: Validate Received Chain

    alt Chain is Valid
        ConsensusA->>NodeA: ReplaceChain()
        NodeA->>NodeA: Replace Local Chain
        NodeA->>StorageA: SaveToFile()
        ConsensusA-->>NodeA: Chain Synced
        Note over NodeA: Chain Length: 7
    else Chain is Invalid
        ConsensusA-->>NodeA: Keep Current Chain
        Note over NodeA: Chain Length: 5
    end

    deactivate ConsensusA
```

## P2P Network Topology

```mermaid
graph TB
    subgraph "Decentralized P2P Mesh Network"
        NodeA[Node A<br/>blockchain-node-1<br/>HTTP: 8081<br/>P2P: 5001]
        NodeB[Node B<br/>blockchain-node-2<br/>HTTP: 8082<br/>P2P: 5001]
        NodeC[Node C<br/>blockchain-node-3<br/>HTTP: 8083<br/>P2P: 5001]
        NodeD[Node D<br/>External Node<br/>HTTP: 8084<br/>P2P: 5001]
    end

    NodeA <-->|P2P Protocol| NodeB
    NodeA <-->|P2P Protocol| NodeC
    NodeA <-->|P2P Protocol| NodeD
    NodeB <-->|P2P Protocol| NodeC
    NodeB <-->|P2P Protocol| NodeD
    NodeC <-->|P2P Protocol| NodeD

    style NodeA fill:#d4edda
    style NodeB fill:#d4edda
    style NodeC fill:#d4edda
    style NodeD fill:#fff3cd
```

## Peer Discovery Flow

```mermaid
flowchart TD
    Start([New Node Starts]) --> LoadConfig[Load Seed Nodes<br/>from Configuration]
    LoadConfig --> ConnectSeeds[Connect to Seed Nodes]
    ConnectSeeds --> Handshake[Perform Handshake]
    Handshake --> ExchangePeers[Exchange Peer Lists]
    ExchangePeers --> DiscoverPeers[Discover New Peers]
    DiscoverPeers --> ConnectPeers{Connect to<br/>Additional Peers}
    ConnectPeers -->|Max Peers<br/>Not Reached| ConnectMore[Connect to New Peer]
    ConnectMore --> ConnectPeers
    ConnectPeers -->|Max Peers<br/>Reached| FullyConnected[Fully Connected<br/>to Network]
    FullyConnected --> SyncChain[Sync Blockchain<br/>from Peers]
    SyncChain --> Ready([Node Ready])

    style Start fill:#e1f5ff
    style Ready fill:#d4edda
    style FullyConnected fill:#fff3cd
```

## Docker Multi-Node Architecture

```mermaid
graph TB
    subgraph "Docker Compose Network: blockchain-network"
        subgraph "Container: blockchain-node-1"
            N1App[ASP.NET Core App]
            N1P2P[P2P Listener :5001]
            N1Vol[(Volume: node-1-data<br/>/app/data)]
            N1App --> N1P2P
            N1App --> N1Vol
        end

        subgraph "Container: blockchain-node-2"
            N2App[ASP.NET Core App]
            N2P2P[P2P Listener :5001]
            N2Vol[(Volume: node-2-data<br/>/app/data)]
            N2App --> N2P2P
            N2App --> N2Vol
        end

        subgraph "Container: blockchain-node-3"
            N3App[ASP.NET Core App]
            N3P2P[P2P Listener :5001]
            N3Vol[(Volume: node-3-data<br/>/app/data)]
            N3App --> N3P2P
            N3App --> N3Vol
        end

        N1P2P <-->|P2P Protocol| N2P2P
        N1P2P <-->|P2P Protocol| N3P2P
        N2P2P <-->|P2P Protocol| N3P2P
    end

    User1[User Browser 1] -->|http://localhost:8081| N1App
    User2[User Browser 2] -->|http://localhost:8082| N2App
    User3[User Browser 3] -->|http://localhost:8083| N3App

    style N1Vol fill:#f8d7da
    style N2Vol fill:#f8d7da
    style N3Vol fill:#f8d7da
```

## Data Flow: Complete Transaction Lifecycle

```mermaid
flowchart TD
    Start([User Submits Transaction]) --> ValidateInput{Validate Input}
    ValidateInput -->|Invalid| Error1[Return Error]
    ValidateInput -->|Valid| CreateTx[Create Transaction Object<br/>Generate TransactionId]

    CreateTx --> AcquireLock[Acquire Write Lock]
    AcquireLock --> AddPending[Add to Pending Pool]
    AddPending --> SaveFile[Save to blockchain.json]
    SaveFile --> Broadcast[Broadcast to Peers]
    Broadcast --> ReleaseLock[Release Write Lock]
    ReleaseLock --> WaitMining[Wait for Mining]

    WaitMining --> MineTriggered{Miner Mines Block}
    MineTriggered --> CreateBlock[Create New Block<br/>with Pending Tx]
    CreateBlock --> PoW[Proof-of-Work<br/>Find Valid Nonce]
    PoW --> ValidateBlock{Validate Block}

    ValidateBlock -->|Invalid| Error2[Discard Block]
    ValidateBlock -->|Valid| AddBlock[Add Block to Chain]
    AddBlock --> ClearPending[Clear Pending Pool]
    ClearPending --> SaveChain[Save Chain to File]
    SaveChain --> BroadcastBlock[Broadcast Block to Peers]

    BroadcastBlock --> PeersReceive[Peers Receive Block]
    PeersReceive --> PeersValidate{Peers Validate}
    PeersValidate -->|Valid| PeersAdd[Peers Add to Chain]
    PeersValidate -->|Invalid| PeersReject[Peers Reject]

    PeersAdd --> Confirmed([Transaction Confirmed])

    style Start fill:#e1f5ff
    style Confirmed fill:#d4edda
    style Error1 fill:#f8d7da
    style Error2 fill:#f8d7da
    style PoW fill:#fff3cd
```

## Thread Safety Architecture

```mermaid
flowchart LR
    subgraph "Multiple Concurrent Operations"
        Op1[API Call:<br/>GetBalance]
        Op2[UI Request:<br/>GetChain]
        Op3[P2P Message:<br/>NewTransaction]
        Op4[Mining:<br/>MinePending]
    end

    subgraph "ReaderWriterLockSlim"
        ReadLock[Read Lock<br/>Multiple Allowed]
        WriteLock[Write Lock<br/>Exclusive Access]
    end

    subgraph "BlockchainNodeService"
        ReadOps[Read Operations<br/>- GetChain<br/>- GetBalance<br/>- IsChainValid]
        WriteOps[Write Operations<br/>- AddTransaction<br/>- MinePending<br/>- ReplaceChain]
    end

    Op1 --> ReadLock
    Op2 --> ReadLock
    Op3 --> WriteLock
    Op4 --> WriteLock

    ReadLock --> ReadOps
    WriteLock --> WriteOps

    ReadOps --> Blockchain[(Blockchain<br/>In-Memory)]
    WriteOps --> Blockchain

    WriteOps --> Storage[(blockchain.json<br/>File)]

    style ReadLock fill:#d4edda
    style WriteLock fill:#fff3cd
    style Blockchain fill:#e1f5ff
    style Storage fill:#f8d7da
```

## Storage Architecture

```mermaid
flowchart TD
    subgraph "In-Memory"
        BCMem[Blockchain Object<br/>Chain + Pending Tx]
    end

    subgraph "File System: /app/data"
        MainFile[(blockchain.json<br/>Main File)]
        TempFile[(blockchain.json.tmp<br/>Temporary File)]

        subgraph "Backups Directory"
            B1[(blockchain_..._1.json)]
            B2[(blockchain_..._2.json)]
            B3[(blockchain_..._3.json)]
            B4[(blockchain_..._4.json)]
            B5[(blockchain_..._5.json)]
        end
    end

    BCMem -->|1. Serialize| JSON[JSON String]
    JSON -->|2. Write| TempFile
    TempFile -->|3. Backup Old| B1
    B1 --> B2
    B2 --> B3
    B3 --> B4
    B4 --> B5
    B5 -->|Delete Oldest| Cleanup[Cleanup]
    TempFile -->|4. Atomic Move| MainFile

    MainFile -->|On Startup<br/>Deserialize| BCMem

    style BCMem fill:#e1f5ff
    style MainFile fill:#d4edda
    style TempFile fill:#fff3cd
    style B1 fill:#f8d7da
    style B2 fill:#f8d7da
    style B3 fill:#f8d7da
    style B4 fill:#f8d7da
    style B5 fill:#f8d7da
```

## API Endpoint Architecture

```mermaid
graph TB
    subgraph "Client Applications"
        Browser[Web Browser<br/>Blazor UI]
        PeerNode[Peer Blockchain Node]
        External[External API Client<br/>curl/Postman]
    end

    subgraph "API Controllers"
        subgraph "User Endpoints"
            Status[GET /api/blockchain/status]
            GetChain[GET /api/blockchain/chain]
            PostTx[POST /api/blockchain/transaction]
            Mine[POST /api/blockchain/mine]
            Balance[GET /api/blockchain/balance/:address]
            GetPeers[GET /api/blockchain/peers]
            ConnectPeer[POST /api/blockchain/peers/connect]
        end

        subgraph "P2P Endpoints"
            Ping[GET /api/node/ping]
            ReceiveTx[POST /api/node/transaction]
            ReceiveBlock[POST /api/node/block]
            SendChain[GET /api/node/chain]
        end
    end

    subgraph "Services"
        BNS[BlockchainNodeService]
        P2P[P2PNode]
        PM[PeerManager]
    end

    Browser --> Status
    Browser --> GetChain
    Browser --> PostTx
    Browser --> Mine
    Browser --> Balance
    Browser --> GetPeers
    Browser --> ConnectPeer

    External --> Status
    External --> PostTx

    PeerNode --> Ping
    PeerNode --> ReceiveTx
    PeerNode --> ReceiveBlock
    PeerNode --> SendChain

    Status --> BNS
    GetChain --> BNS
    PostTx --> BNS
    Mine --> BNS
    Balance --> BNS
    GetPeers --> PM
    ConnectPeer --> P2P

    Ping --> P2P
    ReceiveTx --> BNS
    ReceiveBlock --> BNS
    SendChain --> BNS
```

## Consensus: Longest Chain Rule

```mermaid
stateDiagram-v2
    [*] --> Synchronized: All nodes have same chain

    Synchronized --> Mining1: Node A mines block
    Mining1 --> Broadcasting: Node A broadcasts block
    Broadcasting --> Receiving: Node B receives block
    Receiving --> Validating: Node B validates block

    Validating --> Valid: Block is valid
    Validating --> Invalid: Block is invalid

    Valid --> AddToChain: Add block to local chain
    Invalid --> Reject: Reject and ignore

    AddToChain --> Synchronized: All nodes synced
    Reject --> Synchronized: Keep current chain

    Synchronized --> Fork: Two nodes mine simultaneously
    Fork --> TwoChains: Network has competing chains
    TwoChains --> Syncing: Nodes detect different chains
    Syncing --> Compare: Compare chain lengths

    Compare --> KeepLonger: Accept longer chain
    Compare --> KeepCurrent: Keep current if longer/equal

    KeepLonger --> Replace: Replace local chain
    KeepCurrent --> Maintain: Maintain local chain

    Replace --> Synchronized: Consensus achieved
    Maintain --> Synchronized: Consensus achieved
```

## Block Mining State Machine

```mermaid
stateDiagram-v2
    [*] --> Idle: Node started

    Idle --> CheckPending: Check pending transactions
    CheckPending --> HasPending: Has pending tx
    CheckPending --> Idle: No pending tx

    HasPending --> CreateBlock: Create new block
    CreateBlock --> SetMetadata: Set index, timestamp, previous hash
    SetMetadata --> AddTransactions: Add pending transactions
    AddTransactions --> InitializeNonce: Set nonce = 0

    InitializeNonce --> Mining: Start proof-of-work
    Mining --> CalculateHash: Calculate hash with current nonce
    CalculateHash --> CheckDifficulty: Check if hash meets difficulty

    CheckDifficulty --> ValidHash: Hash starts with required zeros
    CheckDifficulty --> InvalidHash: Hash doesn't meet difficulty

    InvalidHash --> IncrementNonce: nonce++
    IncrementNonce --> Mining: Try again

    ValidHash --> BlockMined: Block successfully mined
    BlockMined --> AddReward: Add mining reward transaction
    AddReward --> AppendChain: Append block to chain
    AppendChain --> ClearPending: Clear pending transactions
    ClearPending --> SaveToDisk: Save blockchain to file
    SaveToDisk --> BroadcastBlock: Broadcast to peers
    BroadcastBlock --> Idle: Ready for next block
```

## Message Flow: Block Propagation

```mermaid
sequenceDiagram
    participant N1 as Node 1<br/>(Miner)
    participant N2 as Node 2
    participant N3 as Node 3
    participant N4 as Node 4

    Note over N1: Mines new block

    N1->>N1: Validate block locally
    N1->>N1: Add to local chain
    N1->>N1: Save to file

    par Broadcast to all peers
        N1->>N2: POST /api/node/block
        N1->>N3: POST /api/node/block
        N1->>N4: POST /api/node/block
    end

    par Nodes validate block
        N2->>N2: Validate block structure
        N3->>N3: Validate block structure
        N4->>N4: Validate block structure
    end

    par Nodes add to chain
        N2->>N2: Add to chain & save
        N3->>N3: Add to chain & save
        N4->>N4: Add to chain & save
    end

    par Send acknowledgment
        N2-->>N1: ACK
        N3-->>N1: ACK
        N4-->>N1: ACK
    end

    Note over N1,N4: All nodes synchronized
```

## Deployment Architecture (Cloud)

```mermaid
graph TB
    subgraph "User Clients"
        User1[User 1]
        User2[User 2]
        User3[User 3]
    end

    subgraph "Cloud Infrastructure"
        subgraph "Region: US-East"
            LB1[Load Balancer<br/>Optional]
            Node1[Blockchain Node 1<br/>Container Instance]
            Vol1[(Persistent Volume<br/>blockchain.json)]
            Node1 --> Vol1
            LB1 --> Node1
        end

        subgraph "Region: EU-West"
            LB2[Load Balancer<br/>Optional]
            Node2[Blockchain Node 2<br/>Container Instance]
            Vol2[(Persistent Volume<br/>blockchain.json)]
            Node2 --> Vol2
            LB2 --> Node2
        end

        subgraph "Region: Asia-Pacific"
            LB3[Load Balancer<br/>Optional]
            Node3[Blockchain Node 3<br/>Container Instance]
            Vol3[(Persistent Volume<br/>blockchain.json)]
            Node3 --> Vol3
            LB3 --> Node3
        end
    end

    User1 --> LB1
    User2 --> LB2
    User3 --> LB3

    Node1 <-->|P2P Mesh| Node2
    Node1 <-->|P2P Mesh| Node3
    Node2 <-->|P2P Mesh| Node3

    style Vol1 fill:#f8d7da
    style Vol2 fill:#f8d7da
    style Vol3 fill:#f8d7da
    style Node1 fill:#d4edda
    style Node2 fill:#d4edda
    style Node3 fill:#d4edda
```

## Application Startup Flow

```mermaid
flowchart TD
    Start([Application Start]) --> LoadConfig[Load Configuration<br/>appsettings.json]
    LoadConfig --> RegisterServices[Register Services<br/>Dependency Injection]

    RegisterServices --> InitStorage[Initialize BlockchainStorage]
    InitStorage --> CheckFile{blockchain.json<br/>exists?}

    CheckFile -->|Yes| LoadChain[Load Blockchain from File]
    CheckFile -->|No| CreateGenesis[Create Genesis Block]

    LoadChain --> ValidateChain{Validate Chain}
    ValidateChain -->|Valid| InitService[Initialize BlockchainNodeService]
    ValidateChain -->|Invalid| Error[Throw Error<br/>Corrupted Chain]

    CreateGenesis --> SaveGenesis[Save Genesis to File]
    SaveGenesis --> InitService

    InitService --> InitP2P[Initialize P2P Node]
    InitP2P --> LoadSeeds[Load Seed Nodes<br/>from Config]
    LoadSeeds --> ConnectSeeds[Connect to Seed Nodes]

    ConnectSeeds --> StartHeartbeat[Start Heartbeat Timer<br/>30s interval]
    StartHeartbeat --> StartSync[Start Sync Timer<br/>60s interval]
    StartSync --> StartWeb[Start Web Server<br/>Blazor + API]

    StartWeb --> Ready([Node Ready])

    style Start fill:#e1f5ff
    style Ready fill:#d4edda
    style Error fill:#f8d7da
```

