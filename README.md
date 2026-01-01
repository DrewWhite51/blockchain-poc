# Blockchain Example Project

An educational blockchain implementation in C# demonstrating core blockchain concepts including Proof-of-Work mining, transaction management, chain validation, and tamper detection.

## Features

- **Proof-of-Work Mining** - Configurable difficulty-based consensus mechanism
- **Transaction System** - SHA256-hashed transactions with sender, recipient, and amount
- **Block Structure** - Complete block implementation with index, timestamp, transactions, previous hash, hash, and nonce
- **Balance Tracking** - Calculate account balances across the entire blockchain
- **Chain Validation** - Verify blockchain integrity and detect tampering
- **Mining Rewards** - Incentivize miners with configurable rewards
- **Genesis Block** - Automatic initialization with network transaction

## Technology Stack

- **Language**: C# with .NET 8.0
- **Cryptography**: System.Security.Cryptography (SHA256)
- **Dependencies**: None - uses only .NET Standard library

## Project Structure

```
blockchain-example-project/
├── Models/
│   ├── Blockchain.cs      # Core blockchain logic, mining, validation
│   ├── Block.cs            # Block structure and proof-of-work
│   └── Transaction.cs      # Transaction model with hashing
├── Core/
│   ├── Wallet.cs           # (Placeholder for future implementation)
│   └── MerkleTree.cs       # (Placeholder for future implementation)
├── Utils/
│   └── HashGenerator.cs    # (Placeholder for future implementation)
└── Program.cs              # Demo application
```

## Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later

### Build and Run

1. Clone or download this repository

2. Navigate to the project directory:
   ```bash
   cd blockchain-example-project
   ```

3. Build the project:
   ```bash
   dotnet build
   ```

4. Run the demo application:
   ```bash
   dotnet run
   ```

### Expected Output

The demo will:
- Create a blockchain with difficulty 4 and 50 coin mining reward
- Generate 11 random transactions between Alice, Bob, Charlie, Diana, and Eve
- Mine blocks as transactions accumulate (every 2 transactions)
- Display final balances for participants and the miner
- Validate the blockchain integrity
- Demonstrate tampering detection by modifying a transaction and re-validating

## How It Works

### Blockchain Basics

Each **block** contains:
- Index (position in chain)
- Timestamp (when created)
- List of transactions
- Previous block's hash (creates the chain)
- Current block's hash (SHA256)
- Nonce (proof-of-work counter)

Blocks are linked together through cryptographic hashing - each block references the previous block's hash, creating an immutable chain.

### Proof-of-Work

Mining requires finding a nonce that produces a hash starting with a specific number of zeros (determined by difficulty). For example, with difficulty 4:
- Target: Hash must start with `0000`
- Miners increment the nonce until a valid hash is found
- This computational effort secures the blockchain

### Transaction Flow

1. **Create Transaction** - Sender, recipient, amount
2. **Add to Pending Pool** - Transactions wait for mining
3. **Mine Block** - Miner collects pending transactions and mines a new block
4. **Receive Reward** - Mining reward transaction added to next block
5. **Validate** - Chain integrity verified through hash validation

### Balance Calculation

Balances are calculated by iterating through all blocks and transactions:
- Add amounts where address is the recipient
- Subtract amounts where address is the sender
- Include mining rewards

### Chain Validation

The blockchain validates itself by checking:
- Each block's hash is correctly calculated
- Each block's previous hash matches the prior block
- Each block's hash meets the proof-of-work difficulty requirement
- Any modification to transaction data invalidates the chain

## Demo Application

The [Program.cs](Program.cs) demonstrates:

1. **Blockchain Creation**
   ```csharp
   BlockChain MyBlockchain = new BlockChain(difficulty: 4, miningReward: 50.0);
   ```

2. **Transaction Generation**
   - 11 random transactions between 5 participants
   - Random amounts from a predefined list

3. **Mining**
   - Blocks mined every 2 transactions
   - Miner "Miner1" receives rewards

4. **Balance Display**
   - Shows final balances for Alice, Bob, Charlie, and Miner1

5. **Validation**
   - Confirms blockchain is valid after legitimate operations
   - Returns `true`

6. **Tampering Detection**
   - Modifies a transaction amount in block 1
   - Re-validates the blockchain
   - Returns `false` - chain correctly detects tampering

## Key Concepts Demonstrated

- **Immutability** - Cryptographic hashing makes the blockchain tamper-evident
- **Proof-of-Work** - Computational effort required to mine blocks
- **Chain Validation** - Verify integrity of the entire blockchain
- **Transaction Management** - Pending pool, mining, and confirmation flow
- **Mining Incentives** - Reward system encourages network participation

## Learning Path

### What This Project Covers

- Fundamental blockchain architecture
- Cryptographic hashing (SHA256)
- Proof-of-Work consensus mechanism
- Transaction lifecycle
- Chain validation and immutability

### What's Missing (Compared to Real Blockchains)

- **Digital Signatures** - Transactions aren't cryptographically signed
- **Merkle Trees** - No efficient transaction verification
- **Peer-to-Peer Network** - Single-node implementation
- **Advanced Consensus** - Only basic PoW, no Proof-of-Stake, etc.
- **Persistent Storage** - Blockchain exists only in memory
- **Smart Contracts** - No programmable transactions

### Possible Enhancements

- Implement digital signatures for transaction security
- Add Merkle tree for efficient transaction verification
- Create a simple P2P network for multi-node operation
- Add persistent storage (database or file system)
- Implement transaction fees
- Add more sophisticated validation rules

## License

This is an educational project for learning blockchain fundamentals.
