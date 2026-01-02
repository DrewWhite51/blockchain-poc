# Phase 1: Core Blockchain Node

## Overview

In this phase, you'll convert the console application into a web application and implement file-based blockchain persistence. Each node will maintain its own local copy of the blockchain stored in a JSON file.

> **💡 Why these features?** See [WHY-THESE-FEATURES.md](WHY-THESE-FEATURES.md#core-architecture-features) for detailed explanations of why file-based persistence, thread safety, and atomic writes are critical.

## Goals

- Update project to ASP.NET Core Web SDK
- Implement file-based blockchain storage
- Create thread-safe node service
- Load/save blockchain from disk

## Step 1: Update Project Configuration

### 1.1 Modify blockchain-example-project.csproj

**Current:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    ...
  </PropertyGroup>
</Project>
```

**Change to:**
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
  </ItemGroup>
</Project>
```

**Changes:**
- SDK changed from `Microsoft.NET.Sdk` to `Microsoft.NET.Sdk.Web`
- Removed `OutputType` (web apps don't need this)
- Added `Newtonsoft.Json` for JSON serialization

### 1.2 Restore Packages

```bash
dotnet restore
dotnet build
```

## Step 2: Create Blockchain Storage

### 2.1 Create Storage Folder

```bash
mkdir Storage
```

### 2.2 Implement BlockchainStorage.cs

**File:** `Storage/BlockchainStorage.cs`

```csharp
using Models;
using Newtonsoft.Json;

namespace Storage;

public class BlockchainStorage
{
    private readonly string _dataDirectory;
    private readonly string _blockchainFilePath;
    private readonly string _backupDirectory;

    public BlockchainStorage(string dataDirectory = "/app/data")
    {
        _dataDirectory = dataDirectory;
        _blockchainFilePath = Path.Combine(_dataDirectory, "blockchain.json");
        _backupDirectory = Path.Combine(_dataDirectory, "backups");

        // Ensure directories exist
        Directory.CreateDirectory(_dataDirectory);
        Directory.CreateDirectory(_backupDirectory);
    }

    /// <summary>
    /// Save blockchain to JSON file with atomic write
    /// </summary>
    public void SaveToFile(BlockChain blockchain)
    {
        try
        {
            // Serialize blockchain
            var json = JsonConvert.SerializeObject(blockchain, Formatting.Indented, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });

            // Write to temporary file first (atomic write)
            var tempFile = _blockchainFilePath + ".tmp";
            File.WriteAllText(tempFile, json);

            // Backup existing file if it exists
            if (File.Exists(_blockchainFilePath))
            {
                var backupFile = Path.Combine(_backupDirectory, $"blockchain_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
                File.Copy(_blockchainFilePath, backupFile, true);

                // Keep only last 5 backups
                CleanupOldBackups();
            }

            // Replace old file with new one
            File.Move(tempFile, _blockchainFilePath, true);

            Console.WriteLine($"[Storage] Blockchain saved to {_blockchainFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Storage] Error saving blockchain: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Load blockchain from JSON file
    /// </summary>
    public BlockChain? LoadFromFile()
    {
        try
        {
            if (!File.Exists(_blockchainFilePath))
            {
                Console.WriteLine("[Storage] No blockchain file found. Will create genesis block.");
                return null;
            }

            var json = File.ReadAllText(_blockchainFilePath);
            var blockchain = JsonConvert.DeserializeObject<BlockChain>(json);

            if (blockchain != null)
            {
                Console.WriteLine($"[Storage] Blockchain loaded: {blockchain.Chain.Count} blocks");
            }

            return blockchain;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Storage] Error loading blockchain: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Check if blockchain file exists
    /// </summary>
    public bool BlockchainExists()
    {
        return File.Exists(_blockchainFilePath);
    }

    /// <summary>
    /// Delete blockchain file (for testing)
    /// </summary>
    public void DeleteBlockchain()
    {
        if (File.Exists(_blockchainFilePath))
        {
            File.Delete(_blockchainFilePath);
            Console.WriteLine("[Storage] Blockchain file deleted");
        }
    }

    /// <summary>
    /// Keep only last 5 backups to save disk space
    /// </summary>
    private void CleanupOldBackups()
    {
        var backupFiles = Directory.GetFiles(_backupDirectory, "blockchain_*.json")
            .OrderByDescending(f => f)
            .Skip(5);

        foreach (var file in backupFiles)
        {
            File.Delete(file);
        }
    }
}
```

**Key Points:**
- **Atomic Writes**: Write to temp file first, then rename (prevents corruption)
- **Backups**: Keep last 5 backups for disaster recovery
- **Error Handling**: Catch and log errors
- **Configurable Path**: Data directory can be changed (important for Docker)

## Step 3: Create Thread-Safe Node Service

### 3.1 Create Services Folder

```bash
mkdir Services
```

### 3.2 Implement BlockchainNodeService.cs

**File:** `Services/BlockchainNodeService.cs`

```csharp
using Models;
using Storage;

namespace Services;

public class BlockchainNodeService
{
    private BlockChain _blockchain;
    private readonly BlockchainStorage _storage;
    private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
    private readonly int _difficulty;
    private readonly double _miningReward;

    public BlockchainNodeService(BlockchainStorage storage, int difficulty = 2, double miningReward = 50.0)
    {
        _storage = storage;
        _difficulty = difficulty;
        _miningReward = miningReward;

        // Initialize blockchain
        _lock.EnterWriteLock();
        try
        {
            var loadedChain = _storage.LoadFromFile();
            if (loadedChain != null)
            {
                _blockchain = loadedChain;
                Console.WriteLine($"[Node] Loaded existing blockchain with {_blockchain.Chain.Count} blocks");
            }
            else
            {
                _blockchain = new BlockChain(_difficulty, _miningReward);
                _storage.SaveToFile(_blockchain);
                Console.WriteLine($"[Node] Created new blockchain with genesis block");
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Get the entire blockchain (thread-safe read)
    /// </summary>
    public BlockChain GetChain()
    {
        _lock.EnterReadLock();
        try
        {
            return _blockchain;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Add a transaction to the pending pool (thread-safe write)
    /// </summary>
    public void AddTransaction(Transaction transaction)
    {
        _lock.EnterWriteLock();
        try
        {
            _blockchain.AddTransaction(transaction);
            _storage.SaveToFile(_blockchain);
            Console.WriteLine($"[Node] Transaction added: {transaction.TransactionId}");
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Mine pending transactions (thread-safe write)
    /// </summary>
    public Block MinePendingTransactions(string minerAddress)
    {
        _lock.EnterWriteLock();
        try
        {
            Console.WriteLine($"[Node] Mining block for {minerAddress}...");
            _blockchain.MinePendingTransactions(minerAddress);

            var latestBlock = _blockchain.GetLatestBlock();
            _storage.SaveToFile(_blockchain);

            Console.WriteLine($"[Node] Block mined! Hash: {latestBlock.Hash}");
            return latestBlock;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Get balance for an address (thread-safe read)
    /// </summary>
    public double GetBalance(string address)
    {
        _lock.EnterReadLock();
        try
        {
            return _blockchain.GetBalance(address);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Get pending transactions (thread-safe read)
    /// </summary>
    public List<Transaction> GetPendingTransactions()
    {
        _lock.EnterReadLock();
        try
        {
            return _blockchain.PendingTransactions.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Validate blockchain integrity (thread-safe read)
    /// </summary>
    public bool IsChainValid()
    {
        _lock.EnterReadLock();
        try
        {
            return _blockchain.IsChainValid();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Get blockchain status
    /// </summary>
    public object GetStatus()
    {
        _lock.EnterReadLock();
        try
        {
            return new
            {
                ChainLength = _blockchain.Chain.Count,
                Difficulty = _blockchain.Difficulty,
                MiningReward = _blockchain.MiningReward,
                PendingTransactions = _blockchain.PendingTransactions.Count,
                IsValid = _blockchain.IsChainValid(),
                LatestBlockHash = _blockchain.GetLatestBlock().Hash
            };
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
}
```

**Key Points:**
- **Thread Safety**: Uses `ReaderWriterLockSlim` for concurrent access
- **Read Lock**: Multiple threads can read simultaneously
- **Write Lock**: Exclusive access for modifications
- **Persistence**: Saves blockchain after each mutation
- **Status Method**: Provides node health information

## Step 4: Testing

### 4.1 Create a Simple Test Program

Update `Program.cs` temporarily to test:

```csharp
using Models;
using Services;
using Storage;

var storage = new BlockchainStorage("./data");
var node = new BlockchainNodeService(storage, difficulty: 2, miningReward: 50.0);

Console.WriteLine("=== Blockchain Node Test ===");
Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(node.GetStatus(), new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

// Add some transactions
node.AddTransaction(new Transaction("Alice", "Bob", 50));
node.AddTransaction(new Transaction("Bob", "Charlie", 25));

// Mine a block
node.MinePendingTransactions("Miner1");

Console.WriteLine("\n=== After Mining ===");
Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(node.GetStatus(), new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine($"\nMiner1 balance: {node.GetBalance("Miner1")}");
Console.WriteLine($"Chain valid: {node.IsChainValid()}");
```

### 4.2 Run Test

```bash
dotnet run
```

**Expected Output:**
```
[Storage] No blockchain file found. Will create genesis block.
[Node] Created new blockchain with genesis block
=== Blockchain Node Test ===
{
  "ChainLength": 1,
  "Difficulty": 2,
  "MiningReward": 50,
  "PendingTransactions": 0,
  "IsValid": true,
  "LatestBlockHash": "..."
}
[Node] Transaction added: ...
[Node] Transaction added: ...
[Node] Mining block for Miner1...
[Node] Block mined! Hash: ...
[Storage] Blockchain saved to ./data/blockchain.json

=== After Mining ===
{
  "ChainLength": 2,
  "Difficulty": 2,
  "MiningReward": 50,
  "PendingTransactions": 0,
  "IsValid": true,
  "LatestBlockHash": "..."
}

Miner1 balance: 50
Chain valid: true
```

### 4.3 Verify File Persistence

```bash
# Check that blockchain.json was created
ls -la ./data/

# View the blockchain file
cat ./data/blockchain.json
```

### 4.4 Test Restart Persistence

```bash
# Run again - should load existing blockchain
dotnet run
```

**Expected:** Should show "Loaded existing blockchain with 2 blocks"

## Completion Checklist

- [ ] Project updated to `Microsoft.NET.Sdk.Web`
- [ ] Newtonsoft.Json package added
- [ ] BlockchainStorage.cs implemented
- [ ] Atomic file writes working
- [ ] Backup system functional
- [ ] BlockchainNodeService.cs implemented
- [ ] Thread safety with ReaderWriterLockSlim
- [ ] Test program runs successfully
- [ ] blockchain.json file created in ./data/
- [ ] Restart loads existing blockchain
- [ ] Backups created in ./data/backups/

## Troubleshooting

### Issue: File permission errors

**Solution:** Ensure data directory has write permissions
```bash
chmod -R 755 ./data
```

### Issue: JSON serialization errors

**Solution:** Check that all blockchain classes are serializable. Add `[JsonIgnore]` to properties that shouldn't be serialized.

### Issue: "File is being used by another process"

**Solution:** Ensure you're using atomic writes (write to .tmp file first)

## Next Steps

Once this phase is complete:
1. Verify blockchain persists across restarts
2. Move on to **Phase 2: P2P Networking** to enable node-to-node communication
3. Keep the test Program.cs - we'll replace it with web host in Phase 3

## How to Start/Stop/Interact

**Start:**
```bash
dotnet run
```

**Stop:**
```
Ctrl+C
```

**Interact:**
- Currently console-only
- Web API will be added in Phase 3
- Use the `BlockchainNodeService` methods directly for now

**View Data:**
```bash
# View blockchain
cat ./data/blockchain.json | jq .

# View backups
ls -la ./data/backups/
```

## Summary

You now have:
- Web-enabled project structure
- File-based blockchain persistence
- Thread-safe node service
- Backup system for disaster recovery
- Foundation for P2P networking (next phase)

The blockchain now survives restarts, just like a real blockchain node!
