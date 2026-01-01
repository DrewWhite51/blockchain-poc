# Blockchain Extensions - Complete Implementation Specifications

This document provides detailed specifications for extending your basic proof-of-work blockchain with advanced features. Each section includes code examples, implementation guidance, and complexity estimates.

---

## Table of Contents

1. [Easy Extensions](#easy-extensions)
   - Transaction Validation
   - Digital Signatures (ECDSA)
   - Merkle Trees
   - Difficulty Adjustment
2. [Medium Complexity](#medium-complexity)
   - Wallet System
   - UTXO Model
   - P2P Networking
   - Smart Contracts
3. [Advanced Features](#advanced-features)
   - Proof of Stake
   - Sharding
4. [Live Demo Implementation](#live-demo-implementation)

---

## EASY EXTENSIONS

### 1. Transaction Validation - Check Sender Balance

**Objective:** Prevent users from spending more than they have by validating balance before adding transactions to the pending pool.

**Time Estimate:** 30-60 minutes

#### Implementation

**New Methods to Add:**
```csharp
// In Blockchain.cs
public bool ValidateTransaction(Transaction transaction)
{
    // Skip validation for mining rewards
    if (transaction.Sender == "network")
        return true;
    
    double senderBalance = GetBalance(transaction.Sender);
    
    // Check if sender has enough balance
    if (senderBalance < transaction.Amount)
    {
        Console.WriteLine($"Invalid transaction: {transaction.Sender} has insufficient balance");
        Console.WriteLine($"Balance: {senderBalance}, Attempted: {transaction.Amount}");
        return false;
    }
    
    return true;
}

// Modify existing AddTransaction method
public void AddTransaction(Transaction transaction)
{
    if (string.IsNullOrEmpty(transaction.Sender) || string.IsNullOrEmpty(transaction.Recipient))
    {
        throw new Exception("Transaction must include sender and recipient");
    }

    if (transaction.Amount <= 0)
    {
        throw new Exception("Transaction amount must be positive");
    }

    // NEW: Validate balance
    if (!ValidateTransaction(transaction))
    {
        throw new Exception("Transaction validation failed: insufficient balance");
    }

    PendingTransactions.Add(transaction);
    Console.WriteLine($"Transaction added to pending pool: {transaction}");
}
```

#### Enhancement - Consider Pending Transactions

```csharp
public double GetAvailableBalance(string address)
{
    double balance = GetBalance(address);
    
    // Subtract pending outgoing transactions
    foreach (var tx in PendingTransactions)
    {
        if (tx.Sender == address)
        {
            balance -= tx.Amount;
        }
    }
    
    return balance;
}
```

#### Edge Cases to Handle
- Mining reward transactions (sender = "network") should bypass validation
- Pending transactions should be considered when checking balance (user can't double-spend from pending pool)
- Genesis block transactions should bypass validation

#### Testing Scenarios
1. User tries to send more than they have → should fail
2. User sends exact balance → should succeed
3. User tries to make multiple pending transactions exceeding balance → should fail
4. Mining rewards should always succeed

---

### 2. Digital Signatures - ECDSA Transaction Signing

**Objective:** Cryptographically prove that transactions are authorized by the sender using public-key cryptography.

**Time Estimate:** 2-3 hours

#### Implementation

**New Classes:**

```csharp
// KeyPair.cs
using System.Security.Cryptography;

public class KeyPair
{
    public string PublicKey { get; set; }
    public string PrivateKey { get; set; }
    
    public KeyPair()
    {
        using (ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256))
        {
            var privateParams = ecdsa.ExportParameters(true);
            var publicParams = ecdsa.ExportParameters(false);
            
            PrivateKey = Convert.ToBase64String(privateParams.D);
            PublicKey = Convert.ToBase64String(publicParams.Q.X.Concat(publicParams.Q.Y).ToArray());
        }
    }
    
    public KeyPair(string privateKey, string publicKey)
    {
        PrivateKey = privateKey;
        PublicKey = publicKey;
    }
}
```

```csharp
// CryptoHelper.cs
using System.Security.Cryptography;
using System.Text;

public static class CryptoHelper
{
    public static string SignTransaction(string privateKey, string transactionData)
    {
        using (ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256))
        {
            // Import private key
            var dBytes = Convert.FromBase64String(privateKey);
            var parameters = new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                D = dBytes
            };
            ecdsa.ImportParameters(parameters);
            
            // Sign the data
            byte[] dataBytes = Encoding.UTF8.GetBytes(transactionData);
            byte[] signature = ecdsa.SignData(dataBytes, HashAlgorithmName.SHA256);
            
            return Convert.ToBase64String(signature);
        }
    }
    
    public static bool VerifySignature(string publicKey, string transactionData, string signature)
    {
        try
        {
            using (ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256))
            {
                // Import public key
                byte[] publicKeyBytes = Convert.FromBase64String(publicKey);
                var parameters = new ECParameters
                {
                    Curve = ECCurve.NamedCurves.nistP256,
                    Q = new ECPoint
                    {
                        X = publicKeyBytes.Take(32).ToArray(),
                        Y = publicKeyBytes.Skip(32).Take(32).ToArray()
                    }
                };
                ecdsa.ImportParameters(parameters);
                
                // Verify signature
                byte[] dataBytes = Encoding.UTF8.GetBytes(transactionData);
                byte[] signatureBytes = Convert.FromBase64String(signature);
                
                return ecdsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256);
            }
        }
        catch
        {
            return false;
        }
    }
}
```

**Modified Transaction Class:**
```csharp
public class Transaction
{
    public string Sender { get; set; }  // Now this is the public key
    public string Recipient { get; set; }
    public double Amount { get; set; }
    public DateTime Timestamp { get; set; }
    public string TransactionId { get; set; }
    public string Signature { get; set; }  // NEW

    public Transaction(string sender, string recipient, double amount)
    {
        Sender = sender;
        Recipient = recipient;
        Amount = amount;
        Timestamp = DateTime.UtcNow;
        TransactionId = CalculateHash();
    }

    public void SignTransaction(string privateKey)
    {
        if (Sender == "network")  // Mining rewards don't need signatures
            return;
            
        string transactionData = $"{Sender}{Recipient}{Amount}{Timestamp}";
        Signature = CryptoHelper.SignTransaction(privateKey, transactionData);
    }

    public bool IsValid()
    {
        // Mining rewards are always valid
        if (Sender == "network")
            return true;

        if (string.IsNullOrEmpty(Signature))
        {
            Console.WriteLine("No signature in transaction");
            return false;
        }

        string transactionData = $"{Sender}{Recipient}{Amount}{Timestamp}";
        return CryptoHelper.VerifySignature(Sender, transactionData, Signature);
    }
    
    public string CalculateHash()
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            string rawData = $"{Sender}{Recipient}{Amount}{Timestamp}";
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            return Convert.ToHexString(bytes);
        }
    }
}
```

**Integration with Blockchain:**
```csharp
// In Blockchain.cs - modify AddTransaction
public void AddTransaction(Transaction transaction)
{
    if (string.IsNullOrEmpty(transaction.Sender) || string.IsNullOrEmpty(transaction.Recipient))
    {
        throw new Exception("Transaction must include sender and recipient");
    }

    // NEW: Verify signature
    if (!transaction.IsValid())
    {
        throw new Exception("Invalid transaction signature");
    }

    if (!ValidateTransaction(transaction))
    {
        throw new Exception("Transaction validation failed: insufficient balance");
    }

    PendingTransactions.Add(transaction);
}
```

#### Usage Example
```csharp
// Create a wallet
var aliceKeys = new KeyPair();

// Create and sign transaction
var tx = new Transaction(aliceKeys.PublicKey, bobKeys.PublicKey, 50);
tx.SignTransaction(aliceKeys.PrivateKey);

blockchain.AddTransaction(tx);
```

#### Security Considerations
- Private keys must NEVER be stored in transactions or blocks
- Use ECDSA (Elliptic Curve) instead of RSA for better performance and smaller signatures
- Public keys serve as addresses (like Ethereum)

---

### 3. Merkle Trees - Efficient Transaction Verification

**Objective:** Create a hash tree structure that allows efficient proof that a transaction is included in a block without downloading all transactions.

**Time Estimate:** 3-4 hours

#### Implementation

**New Class:**

```csharp
// MerkleTree.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

public class MerkleTree
{
    public string Root { get; private set; }
    private List<string> Leaves { get; set; }

    public MerkleTree(List<Transaction> transactions)
    {
        Leaves = transactions.Select(t => t.TransactionId).ToList();
        Root = BuildTree(Leaves);
    }

    private string BuildTree(List<string> hashes)
    {
        if (hashes.Count == 0)
            return HashString("empty");
            
        if (hashes.Count == 1)
            return hashes[0];

        List<string> newLevel = new List<string>();

        // Process pairs
        for (int i = 0; i < hashes.Count; i += 2)
        {
            string left = hashes[i];
            string right = (i + 1 < hashes.Count) ? hashes[i + 1] : hashes[i]; // Duplicate if odd
            
            string combined = CombineHash(left, right);
            newLevel.Add(combined);
        }

        return BuildTree(newLevel);
    }

    private string CombineHash(string left, string right)
    {
        return HashString(left + right);
    }

    private string HashString(string input)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes);
        }
    }

    // Generate proof that a transaction is in the tree
    public List<string> GetProof(string transactionId)
    {
        List<string> proof = new List<string>();
        
        if (!Leaves.Contains(transactionId))
            return proof;

        var currentLevel = new List<string>(Leaves);
        int index = currentLevel.IndexOf(transactionId);

        while (currentLevel.Count > 1)
        {
            List<string> nextLevel = new List<string>();
            
            for (int i = 0; i < currentLevel.Count; i += 2)
            {
                string left = currentLevel[i];
                string right = (i + 1 < currentLevel.Count) ? currentLevel[i + 1] : currentLevel[i];
                
                // If this pair contains our target, add sibling to proof
                if (i == index || i + 1 == index)
                {
                    string sibling = (i == index) ? right : left;
                    proof.Add(sibling);
                    index = i / 2; // Update index for next level
                }
                
                nextLevel.Add(CombineHash(left, right));
            }
            
            currentLevel = nextLevel;
        }

        return proof;
    }

    // Verify a transaction is in the tree using proof
    public static bool VerifyProof(string transactionId, List<string> proof, string merkleRoot)
    {
        string currentHash = transactionId;

        foreach (string sibling in proof)
        {
            currentHash = HashString(currentHash + sibling);
        }

        return currentHash == merkleRoot;
    }

    private static string HashString(string input)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes);
        }
    }
}
```

**Modified Block Class:**
```csharp
public class Block
{
    // ... existing properties ...
    public string MerkleRoot { get; set; }  // NEW
    private MerkleTree merkleTree;  // NEW

    public Block(int index, List<Transaction> transactions, string previousHash)
    {
        Index = index;
        Timestamp = DateTime.UtcNow;
        Transactions = transactions;
        PreviousHash = previousHash;
        
        // NEW: Build Merkle tree
        merkleTree = new MerkleTree(transactions);
        MerkleRoot = merkleTree.Root;
        
        Nonce = 0;
        Hash = CalculateHash();
    }

    public string CalculateHash()
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            // Use Merkle root instead of concatenating all transactions
            string rawData = $"{Index}{Timestamp}{MerkleRoot}{PreviousHash}{Nonce}";
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            return Convert.ToHexString(bytes);
        }
    }

    // NEW: Get proof for a transaction
    public List<string> GetTransactionProof(string transactionId)
    {
        return merkleTree.GetProof(transactionId);
    }

    // NEW: Verify transaction is in block
    public bool VerifyTransaction(string transactionId, List<string> proof)
    {
        return MerkleTree.VerifyProof(transactionId, proof, MerkleRoot);
    }
}
```

#### Usage Example
```csharp
// After mining a block
Block block = blockchain.Chain[1];
string txId = block.Transactions[0].TransactionId;

// Generate proof
List<string> proof = block.GetTransactionProof(txId);

// Verify (can be done without full block data!)
bool isValid = block.VerifyTransaction(txId, proof);
Console.WriteLine($"Transaction in block: {isValid}");
```

#### Benefits
- **Proof size:** O(log n) instead of O(n)
- **Verification:** Don't need all transactions to verify one
- **SPV (Simplified Payment Verification):** Light clients can verify transactions
- **Same as Ethereum:** Ethereum uses Merkle Patricia Tries

---

### 4. Difficulty Adjustment - Auto-adjust Based on Block Time

**Objective:** Automatically adjust mining difficulty to maintain consistent block times (like Bitcoin's ~10 minute target).

**Time Estimate:** 1-2 hours

#### Implementation

**Constants and Configuration:**
```csharp
// In Blockchain.cs
public class Blockchain
{
    // ... existing properties ...
    public int TargetBlockTime { get; set; } // In seconds
    public int DifficultyAdjustmentInterval { get; set; } // Adjust every N blocks
    private const int MinDifficulty = 1;
    private const int MaxDifficulty = 10;

    public Blockchain(int difficulty = 2, double miningReward = 50, int targetBlockTime = 30)
    {
        Chain = new List<Block>();
        Difficulty = difficulty;
        PendingTransactions = new List<Transaction>();
        MiningReward = miningReward;
        TargetBlockTime = targetBlockTime; // 30 seconds per block
        DifficultyAdjustmentInterval = 10; // Adjust every 10 blocks
        
        Chain.Add(CreateGenesisBlock());
    }
}
```

**Difficulty Adjustment Logic:**
```csharp
// In Blockchain.cs
public void AdjustDifficulty()
{
    // Only adjust at intervals
    if (Chain.Count % DifficultyAdjustmentInterval != 0)
        return;

    // Need at least the interval number of blocks
    if (Chain.Count < DifficultyAdjustmentInterval)
        return;

    // Get the last adjustment interval blocks
    var recentBlocks = Chain
        .Skip(Chain.Count - DifficultyAdjustmentInterval)
        .Take(DifficultyAdjustmentInterval)
        .ToList();

    // Calculate actual time taken
    DateTime startTime = recentBlocks.First().Timestamp;
    DateTime endTime = recentBlocks.Last().Timestamp;
    double actualTime = (endTime - startTime).TotalSeconds;

    // Calculate expected time
    double expectedTime = TargetBlockTime * DifficultyAdjustmentInterval;

    // Calculate ratio
    double ratio = actualTime / expectedTime;

    int oldDifficulty = Difficulty;

    // Adjust difficulty
    if (ratio < 0.5)
    {
        // Blocks too fast, increase difficulty
        Difficulty = Math.Min(Difficulty + 1, MaxDifficulty);
    }
    else if (ratio > 2.0)
    {
        // Blocks too slow, decrease difficulty
        Difficulty = Math.Max(Difficulty - 1, MinDifficulty);
    }
    // If between 0.5 and 2.0, keep difficulty the same

    if (oldDifficulty != Difficulty)
    {
        Console.WriteLine($"\n=== DIFFICULTY ADJUSTMENT ===");
        Console.WriteLine($"Last {DifficultyAdjustmentInterval} blocks took {actualTime:F2}s (expected {expectedTime}s)");
        Console.WriteLine($"Difficulty: {oldDifficulty} -> {Difficulty}");
        Console.WriteLine($"=============================\n");
    }
}

// Modify MinePendingTransactions to call adjustment
public void MinePendingTransactions(string minerAddress)
{
    // Adjust difficulty before mining
    AdjustDifficulty();
    
    PendingTransactions.Add(new Transaction("network", minerAddress, MiningReward));
    
    Block newBlock = new Block(Chain.Count, PendingTransactions, GetLatestBlock().Hash);
    newBlock.MineBlock(Difficulty);
    
    Chain.Add(newBlock);
    
    PendingTransactions = new List<Transaction>();
}
```

**Statistics Tracking:**
```csharp
// Add method to analyze blockchain performance
public void PrintMiningStatistics()
{
    if (Chain.Count < 2)
    {
        Console.WriteLine("Not enough blocks for statistics");
        return;
    }

    Console.WriteLine("\n=== MINING STATISTICS ===");
    
    double totalTime = 0;
    var blockTimes = new List<double>();
    
    for (int i = 1; i < Chain.Count; i++)
    {
        double blockTime = (Chain[i].Timestamp - Chain[i - 1].Timestamp).TotalSeconds;
        blockTimes.Add(blockTime);
        totalTime += blockTime;
    }

    Console.WriteLine($"Total Blocks: {Chain.Count}");
    Console.WriteLine($"Average Block Time: {totalTime / blockTimes.Count:F2}s (target: {TargetBlockTime}s)");
    Console.WriteLine($"Current Difficulty: {Difficulty}");
    Console.WriteLine($"Fastest Block: {blockTimes.Min():F2}s");
    Console.WriteLine($"Slowest Block: {blockTimes.Max():F2}s");
    Console.WriteLine("========================\n");
}
```

#### Testing Scenarios
1. Mine blocks rapidly → difficulty should increase
2. Wait between blocks → difficulty should decrease
3. Difficulty should stay within min/max bounds
4. Average block time should approach target over time

---

## MEDIUM COMPLEXITY

### 5. Wallet System - Public/Private Key Management

**Objective:** Create a wallet that manages key pairs, signs transactions, and tracks balance.

**Time Estimate:** 4-6 hours

#### Implementation

**Wallet Class:**
```csharp
// Wallet.cs
using System;
using System.Collections.Generic;
using System.Linq;

public class Wallet
{
    public string Address { get; private set; } // Public key
    private string PrivateKey { get; set; }
    public double Balance { get; private set; }
    private Blockchain blockchain;

    public Wallet(Blockchain blockchain)
    {
        this.blockchain = blockchain;
        var keyPair = new KeyPair();
        Address = keyPair.PublicKey;
        PrivateKey = keyPair.PrivateKey;
        UpdateBalance();
    }

    // Load existing wallet from keys
    public Wallet(Blockchain blockchain, string privateKey, string publicKey)
    {
        this.blockchain = blockchain;
        PrivateKey = privateKey;
        Address = publicKey;
        UpdateBalance();
    }

    public void UpdateBalance()
    {
        Balance = blockchain.GetBalance(Address);
    }

    public Transaction CreateTransaction(string recipient, double amount)
    {
        UpdateBalance();
        
        if (Balance < amount)
        {
            throw new Exception($"Insufficient funds. Balance: {Balance}, Attempted: {amount}");
        }

        var transaction = new Transaction(Address, recipient, amount);
        transaction.SignTransaction(PrivateKey);
        
        return transaction;
    }

    public void SendTransaction(string recipient, double amount)
    {
        var transaction = CreateTransaction(recipient, amount);
        blockchain.AddTransaction(transaction);
        Console.WriteLine($"Transaction sent: {amount} to {recipient.Substring(0, 10)}...");
    }

    public List<Transaction> GetTransactionHistory()
    {
        var history = new List<Transaction>();
        
        foreach (var block in blockchain.Chain)
        {
            foreach (var tx in block.Transactions)
            {
                if (tx.Sender == Address || tx.Recipient == Address)
                {
                    history.Add(tx);
                }
            }
        }
        
        return history;
    }

    public void PrintWalletInfo()
    {
        Console.WriteLine("\n=== WALLET INFO ===");
        Console.WriteLine($"Address: {Address.Substring(0, 20)}...");
        Console.WriteLine($"Balance: {Balance}");
        Console.WriteLine("===================\n");
    }

    public void PrintTransactionHistory()
    {
        var history = GetTransactionHistory();
        
        Console.WriteLine("\n=== TRANSACTION HISTORY ===");
        foreach (var tx in history)
        {
            string type = tx.Sender == Address ? "SENT" : "RECEIVED";
            string counterparty = tx.Sender == Address ? tx.Recipient : tx.Sender;
            string amount = tx.Sender == Address ? $"-{tx.Amount}" : $"+{tx.Amount}";
            
            Console.WriteLine($"{tx.Timestamp:yyyy-MM-dd HH:mm:ss} | {type} | {amount} | {counterparty.Substring(0, 10)}...");
        }
        Console.WriteLine("===========================\n");
    }

    // Export wallet (DANGER: exposes private key)
    public WalletExport Export()
    {
        Console.WriteLine("WARNING: This will expose your private key!");
        return new WalletExport
        {
            Address = Address,
            PrivateKey = PrivateKey
        };
    }
}

public class WalletExport
{
    public string Address { get; set; }
    public string PrivateKey { get; set; }
}
```

**Wallet Manager (Multiple Wallets):**
```csharp
// WalletManager.cs
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

public class WalletManager
{
    private Dictionary<string, Wallet> wallets;
    private Blockchain blockchain;
    private string walletDirectory = "wallets/";

    public WalletManager(Blockchain blockchain)
    {
        this.blockchain = blockchain;
        wallets = new Dictionary<string, Wallet>();
        
        if (!Directory.Exists(walletDirectory))
            Directory.CreateDirectory(walletDirectory);
    }

    public Wallet CreateWallet(string name)
    {
        var wallet = new Wallet(blockchain);
        wallets[name] = wallet;
        SaveWallet(name, wallet);
        
        Console.WriteLine($"Created wallet '{name}' with address {wallet.Address.Substring(0, 20)}...");
        return wallet;
    }

    public Wallet LoadWallet(string name)
    {
        string filepath = Path.Combine(walletDirectory, $"{name}.json");
        
        if (!File.Exists(filepath))
            throw new Exception($"Wallet '{name}' not found");

        string json = File.ReadAllText(filepath);
        var export = JsonSerializer.Deserialize<WalletExport>(json);
        
        var wallet = new Wallet(blockchain, export.PrivateKey, export.Address);
        wallets[name] = wallet;
        
        Console.WriteLine($"Loaded wallet '{name}'");
        return wallet;
    }

    private void SaveWallet(string name, Wallet wallet)
    {
        string filepath = Path.Combine(walletDirectory, $"{name}.json");
        var export = wallet.Export();
        string json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filepath, json);
    }

    public Wallet GetWallet(string name)
    {
        if (wallets.ContainsKey(name))
            return wallets[name];
        
        return LoadWallet(name);
    }

    public List<string> ListWallets()
    {
        var files = Directory.GetFiles(walletDirectory, "*.json");
        return files.Select(f => Path.GetFileNameWithoutExtension(f)).ToList();
    }
}
```

#### Usage Example
```csharp
// Create wallet manager
var walletManager = new WalletManager(blockchain);

// Create wallets
var aliceWallet = walletManager.CreateWallet("alice");
var bobWallet = walletManager.CreateWallet("bob");

// Send transaction
aliceWallet.SendTransaction(bobWallet.Address, 50);

// Mine block
blockchain.MinePendingTransactions(aliceWallet.Address);

// Check balance
aliceWallet.UpdateBalance();
aliceWallet.PrintWalletInfo();
aliceWallet.PrintTransactionHistory();
```

---

### 6. UTXO Model - Bitcoin-style Transaction Model

**Objective:** Implement Unspent Transaction Output model instead of account-based model (major architectural change).

**Time Estimate:** 8-12 hours

#### Conceptual Overview
- **Account model (current):** Track balance per address (like a bank account)
- **UTXO model:** Track unspent outputs from previous transactions (like cash)

#### Core Classes

```csharp
// TransactionOutput.cs
public class TransactionOutput
{
    public string TransactionId { get; set; }
    public int OutputIndex { get; set; }
    public string Recipient { get; set; }
    public double Amount { get; set; }
    public bool IsSpent { get; set; }

    public TransactionOutput(string txId, int index, string recipient, double amount)
    {
        TransactionId = txId;
        OutputIndex = index;
        Recipient = recipient;
        Amount = amount;
        IsSpent = false;
    }

    public string GetId()
    {
        return $"{TransactionId}:{OutputIndex}";
    }
}
```

```csharp
// TransactionInput.cs
public class TransactionInput
{
    public string PreviousTransactionId { get; set; }
    public int OutputIndex { get; set; }
    public string Signature { get; set; }

    public TransactionInput(string prevTxId, int outputIndex)
    {
        PreviousTransactionId = prevTxId;
        OutputIndex = outputIndex;
    }

    public string GetReferencedOutputId()
    {
        return $"{PreviousTransactionId}:{OutputIndex}";
    }
}
```

```csharp
// UTXOTransaction.cs
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

public class UTXOTransaction
{
    public string TransactionId { get; set; }
    public List<TransactionInput> Inputs { get; set; }
    public List<TransactionOutput> Outputs { get; set; }
    public DateTime Timestamp { get; set; }

    public UTXOTransaction()
    {
        Inputs = new List<TransactionInput>();
        Outputs = new List<TransactionOutput>();
        Timestamp = DateTime.UtcNow;
    }

    public void AddInput(TransactionInput input)
    {
        Inputs.Add(input);
    }

    public void AddOutput(string recipient, double amount)
    {
        Outputs.Add(new TransactionOutput(TransactionId, Outputs.Count, recipient, amount));
    }

    public void FinalizeTransaction()
    {
        TransactionId = CalculateHash();
        
        // Update output transaction IDs
        foreach (var output in Outputs)
        {
            output.TransactionId = TransactionId;
        }
    }

    private string CalculateHash()
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            string inputData = string.Join("", Inputs.Select(i => i.GetReferencedOutputId()));
            string outputData = string.Join("", Outputs.Select(o => $"{o.Recipient}{o.Amount}"));
            string rawData = $"{inputData}{outputData}{Timestamp}";
            
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            return Convert.ToHexString(bytes);
        }
    }

    public double GetInputValue(UTXOPool pool)
    {
        double total = 0;
        foreach (var input in Inputs)
        {
            var utxo = pool.GetUTXO(input.GetReferencedOutputId());
            if (utxo != null)
                total += utxo.Amount;
        }
        return total;
    }

    public double GetOutputValue()
    {
        return Outputs.Sum(o => o.Amount);
    }

    public bool Verify(UTXOPool pool, string senderAddress)
    {
        // Check inputs exist and are unspent
        foreach (var input in Inputs)
        {
            var utxo = pool.GetUTXO(input.GetReferencedOutputId());
            if (utxo == null || utxo.IsSpent || utxo.Recipient != senderAddress)
                return false;
        }

        // Check input value >= output value
        return GetInputValue(pool) >= GetOutputValue();
    }
}
```

**UTXO Pool:**
```csharp
// UTXOPool.cs
using System.Collections.Generic;

public class UTXOPool
{
    private Dictionary<string, TransactionOutput> pool;

    public UTXOPool()
    {
        pool = new Dictionary<string, TransactionOutput>();
    }

    public void AddUTXO(TransactionOutput output)
    {
        pool[output.GetId()] = output;
    }

    public void RemoveUTXO(string outputId)
    {
        if (pool.ContainsKey(outputId))
        {
            pool[outputId].IsSpent = true;
            pool.Remove(outputId);
        }
    }

    public TransactionOutput GetUTXO(string outputId)
    {
        return pool.ContainsKey(outputId) ? pool[outputId] : null;
    }

    public List<TransactionOutput> GetUTXOsForAddress(string address)
    {
        var utxos = new List<TransactionOutput>();
        foreach (var utxo in pool.Values)
        {
            if (utxo.Recipient == address && !utxo.IsSpent)
            {
                utxos.Add(utxo);
            }
        }
        return utxos;
    }

    public double GetBalance(string address)
    {
        return GetUTXOsForAddress(address).Sum(u => u.Amount);
    }

    public void ProcessTransaction(UTXOTransaction transaction)
    {
        // Remove spent UTXOs
        foreach (var input in transaction.Inputs)
        {
            RemoveUTXO(input.GetReferencedOutputId());
        }

        // Add new UTXOs
        foreach (var output in transaction.Outputs)
        {
            AddUTXO(output);
        }
    }
}
```

#### Key Differences

| Aspect | Account Model | UTXO Model |
|--------|--------------|------------|
| State | Balance per address | Set of unspent outputs |
| Transaction | "Send X from A to B" | "Spend outputs, create new outputs" |
| Balance | Direct lookup | Sum of unspent outputs |
| Privacy | Poor | Better (new addresses) |
| Complexity | Simpler | More complex |

---

### 7. P2P Networking - Multiple Communicating Nodes

**Objective:** Create a peer-to-peer network where multiple nodes can communicate, share blocks, and reach consensus.

**Time Estimate:** 10-15 hours

#### Architecture Overview
```
Node 1 (Port 5000)  <-->  Node 2 (Port 5001)  <-->  Node 3 (Port 5002)
     ^                          ^                          ^
     |                          |                          |
     +--------------------------|---------------------------+
                          (Gossip Protocol)
```

#### Core Components

```csharp
// Peer.cs
public class Peer
{
    public string Address { get; set; }  // IP:Port
    public DateTime LastSeen { get; set; }
    public int ChainLength { get; set; }

    public Peer(string address)
    {
        Address = address;
        LastSeen = DateTime.UtcNow;
    }
}
```

```csharp
// NetworkMessage.cs
public enum MessageType
{
    NewBlock,
    NewTransaction,
    RequestChain,
    ResponseChain,
    RequestPeers,
    ResponsePeers,
    Ping,
    Pong
}

public class NetworkMessage
{
    public MessageType Type { get; set; }
    public string Payload { get; set; }  // JSON serialized data
    public string SenderId { get; set; }
    public DateTime Timestamp { get; set; }

    public NetworkMessage(MessageType type, string payload, string senderId)
    {
        Type = type;
        Payload = payload;
        SenderId = senderId;
        Timestamp = DateTime.UtcNow;
    }
}
```

**P2P Node:**
```csharp
// P2PNode.cs
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class P2PNode
{
    public string NodeId { get; private set; }
    public int Port { get; private set; }
    public Blockchain Blockchain { get; private set; }
    
    private List<Peer> peers;
    private TcpListener listener;
    private bool isRunning;
    private Dictionary<string, DateTime> seenMessages;

    public P2PNode(int port, Blockchain blockchain)
    {
        NodeId = Guid.NewGuid().ToString();
        Port = port;
        Blockchain = blockchain;
        peers = new List<Peer>();
        seenMessages = new Dictionary<string, DateTime>();
    }

    public async Task Start()
    {
        isRunning = true;
        listener = new TcpListener(IPAddress.Any, Port);
        listener.Start();
        
        Console.WriteLine($"Node {NodeId.Substring(0, 8)} started on port {Port}");

        _ = Task.Run(() => ListenForConnections());
        _ = Task.Run(() => MaintainPeers());
    }

    private async Task ListenForConnections()
    {
        while (isRunning)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClient(client));
            }
            catch (Exception ex)
            {
                if (isRunning)
                    Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }

    private async Task HandleClient(TcpClient client)
    {
        try
        {
            using (var stream = client.GetStream())
            {
                byte[] buffer = new byte[8192];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                
                if (bytesRead > 0)
                {
                    string json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    var message = JsonSerializer.Deserialize<NetworkMessage>(json);
                    await ProcessMessage(message);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client: {ex.Message}");
        }
        finally
        {
            client.Close();
        }
    }

    private async Task ProcessMessage(NetworkMessage message)
    {
        string messageId = $"{message.SenderId}:{message.Timestamp}";
        if (seenMessages.ContainsKey(messageId))
            return;
        
        seenMessages[messageId] = DateTime.UtcNow;

        switch (message.Type)
        {
            case MessageType.NewBlock:
                await HandleNewBlock(message);
                break;
            case MessageType.NewTransaction:
                await HandleNewTransaction(message);
                break;
            // ... handle other message types
        }
    }

    public async Task ConnectToPeer(string address)
    {
        var peer = new Peer(address);
        peers.Add(peer);
        Console.WriteLine($"Connected to peer at {address}");
    }

    public async Task BroadcastNewBlock(Block block)
    {
        var message = new NetworkMessage(
            MessageType.NewBlock,
            JsonSerializer.Serialize(block),
            NodeId
        );
        
        await BroadcastMessage(message, null);
    }

    // ... additional networking methods
}
```

#### Key P2P Concepts
1. **Gossip Protocol**: Nodes broadcast messages to neighbors
2. **Peer Discovery**: Nodes share their peer lists
3. **Chain Synchronization**: Longest valid chain wins
4. **Message Deduplication**: Track seen messages
5. **Heartbeat/Ping**: Maintain connectivity

---

### 8. Smart Contract Basics - Simple Script Execution

**Objective:** Add programmable logic that executes on-chain.

**Time Estimate:** 12-16 hours

#### Contract Structure

```csharp
// SmartContract.cs
public class SmartContract
{
    public string Address { get; set; }
    public string Code { get; set; }
    public Dictionary<string, object> Storage { get; set; }
    public string Creator { get; set; }
    public DateTime CreatedAt { get; set; }

    public SmartContract(string creator, string code)
    {
        Creator = creator;
        Code = code;
        Storage = new Dictionary<string, object>();
        CreatedAt = DateTime.UtcNow;
        Address = GenerateAddress();
    }

    private string GenerateAddress()
    {
        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            string data = $"{Creator}{Code}{CreatedAt}";
            byte[] hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(hash).Substring(0, 40);
        }
    }
}
```

**Simple DSL:**
```csharp
// ContractInstruction.cs
public enum InstructionType
{
    SET,      // SET key value
    GET,      // GET key
    ADD,      // ADD key amount
    SUB,      // SUB key amount
    TRANSFER, // TRANSFER recipient amount
    REQUIRE   // REQUIRE condition message
}
```

**Example Contract:**
```csharp
string tokenContract = @"
function mint
    REQUIRE $amount > 0 'Amount must be positive'
    ADD balances.$msg.sender $amount
    ADD totalSupply $amount

function transfer
    REQUIRE balances.$msg.sender >= $amount 'Insufficient balance'
    SUB balances.$msg.sender $amount
    ADD balances.$to $amount

function balanceOf
    GET balances.$address
";
```

---

## ADVANCED FEATURES

### 9. Proof of Stake

**Objective:** Implement PoS as alternative to PoW.

**Time Estimate:** 15-20 hours

#### Key Components

```csharp
// Validator.cs
public class Validator
{
    public string Address { get; set; }
    public double Stake { get; set; }
    public int BlocksValidated { get; set; }
    public DateTime StakedAt { get; set; }

    public double GetWeight()
    {
        double timeBonus = (DateTime.UtcNow - StakedAt).TotalDays * 0.01;
        return Stake * (1 + Math.Min(timeBonus, 0.5));
    }
}
```

```csharp
// StakePool.cs
public class StakePool
{
    public Dictionary<string, Validator> Validators { get; private set; }
    public double MinimumStake { get; set; }

    public string SelectValidator(string randomSeed)
    {
        // Weighted random selection based on stake
        var activeValidators = Validators.Values.Where(v => v.IsActive).ToList();
        double totalWeight = activeValidators.Sum(v => v.GetWeight());
        
        var random = new Random(randomSeed.GetHashCode());
        double randomValue = random.NextDouble() * totalWeight;

        double cumulative = 0;
        foreach (var validator in activeValidators)
        {
            cumulative += validator.GetWeight();
            if (randomValue <= cumulative)
                return validator.Address;
        }

        return activeValidators.Last().Address;
    }
}
```

**PoS vs PoW:**

| Aspect | Proof of Work | Proof of Stake |
|--------|--------------|----------------|
| Energy | High | Low |
| Speed | Slow | Fast |
| Hardware | ASICs | Normal |
| Security | 51% hash power | 51% stake |

---

### 10. Sharding - Parallel Processing

**Objective:** Process transactions in parallel across multiple chains.

**Time Estimate:** 20+ hours

#### Shard Structure

```csharp
// Shard.cs
public class Shard
{
    public int ShardId { get; set; }
    public List<Block> Chain { get; set; }
    public HashSet<string> AssignedAddresses { get; set; }

    public bool IsResponsibleFor(string address)
    {
        return AssignedAddresses.Contains(address);
    }

    public void ProcessTransaction(Transaction tx)
    {
        if (!IsResponsibleFor(tx.Sender))
            throw new Exception("Not responsible for this transaction");
        
        // Process in this shard
    }
}
```

**Beacon Chain:**
```csharp
// BeaconChain.cs
public class BeaconChain
{
    public Dictionary<int, Shard> Shards { get; set; }
    public int NumberOfShards { get; set; }

    public int AssignAddressToShard(string address)
    {
        // Deterministic assignment
        int shardId = Math.Abs(address.GetHashCode()) % NumberOfShards;
        Shards[shardId].AssignAddress(address);
        return shardId;
    }

    public void ProcessCrossShardTransaction(Transaction tx)
    {
        // Handle transaction across shards
        int senderShard = AssignAddressToShard(tx.Sender);
        int recipientShard = AssignAddressToShard(tx.Recipient);
        
        // Coordinate between shards
    }
}
```

---

## LIVE DEMO IMPLEMENTATION

This section provides specifications for creating an interactive web-based demo of your blockchain.

### Demo Architecture

```
┌─────────────────────────────────────────────────┐
│           Web Frontend (React/HTML)             │
│  - Visual blockchain display                    │
│  - Transaction form                             │
│  - Mining controls                              │
│  - Network visualization                        │
└─────────────────────┬───────────────────────────┘
                      │ HTTP/WebSocket
┌─────────────────────┴───────────────────────────┐
│         ASP.NET Core Web API Backend            │
│  - Blockchain REST API                          │
│  - WebSocket for real-time updates              │
│  - Multiple node simulation                     │
└─────────────────────┬───────────────────────────┘
                      │
┌─────────────────────┴───────────────────────────┐
│         C# Blockchain Implementation            │
│  - Your blockchain classes                      │
│  - Mining logic                                 │
│  - P2P simulation                               │
└─────────────────────────────────────────────────┘
```

### Backend API Implementation

**Project Structure:**
```
BlockchainDemo/
├── BlockchainDemo.API/
│   ├── Controllers/
│   │   ├── BlockchainController.cs
│   │   ├── TransactionController.cs
│   │   ├── MiningController.cs
│   │   └── NetworkController.cs
│   ├── Hubs/
│   │   └── BlockchainHub.cs
│   ├── Models/
│   │   └── (Your blockchain classes)
│   └── Program.cs
└── BlockchainDemo.Web/
    ├── index.html
    ├── styles.css
    └── app.js
```

#### Step 1: Create ASP.NET Core API

```bash
# Create new ASP.NET Core Web API
dotnet new webapi -n BlockchainDemo.API
cd BlockchainDemo.API

# Add SignalR for WebSocket support
dotnet add package Microsoft.AspNetCore.SignalR
```

#### Step 2: Blockchain Controller

```csharp
// Controllers/BlockchainController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

[ApiController]
[Route("api/[controller]")]
public class BlockchainController : ControllerBase
{
    private static Blockchain blockchain = new Blockchain(difficulty: 3);
    private readonly IHubContext<BlockchainHub> hubContext;

    public BlockchainController(IHubContext<BlockchainHub> hubContext)
    {
        this.hubContext = hubContext;
    }

    [HttpGet("chain")]
    public ActionResult<object> GetChain()
    {
        return Ok(new
        {
            length = blockchain.Chain.Count,
            chain = blockchain.Chain.Select(b => new
            {
                index = b.Index,
                timestamp = b.Timestamp,
                transactions = b.Transactions,
                hash = b.Hash,
                previousHash = b.PreviousHash,
                nonce = b.Nonce
            })
        });
    }

    [HttpGet("block/{index}")]
    public ActionResult<object> GetBlock(int index)
    {
        if (index < 0 || index >= blockchain.Chain.Count)
            return NotFound();

        var block = blockchain.Chain[index];
        return Ok(new
        {
            index = block.Index,
            timestamp = block.Timestamp,
            transactions = block.Transactions,
            hash = block.Hash,
            previousHash = block.PreviousHash,
            nonce = block.Nonce
        });
    }

    [HttpGet("validate")]
    public ActionResult<object> ValidateChain()
    {
        return Ok(new { valid = blockchain.IsChainValid() });
    }

    [HttpGet("balance/{address}")]
    public ActionResult<object> GetBalance(string address)
    {
        return Ok(new { address, balance = blockchain.GetBalance(address) });
    }

    [HttpGet("stats")]
    public ActionResult<object> GetStats()
    {
        return Ok(new
        {
            totalBlocks = blockchain.Chain.Count,
            difficulty = blockchain.Difficulty,
            pendingTransactions = blockchain.PendingTransactions.Count,
            isValid = blockchain.IsChainValid()
        });
    }
}
```

#### Step 3: Transaction Controller

```csharp
// Controllers/TransactionController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

[ApiController]
[Route("api/[controller]")]
public class TransactionController : ControllerBase
{
    private static Blockchain blockchain;
    private readonly IHubContext<BlockchainHub> hubContext;

    public TransactionController(IHubContext<BlockchainHub> hubContext)
    {
        this.hubContext = hubContext;
        // Get shared blockchain instance
        blockchain = BlockchainService.Instance;
    }

    [HttpPost]
    public async Task<ActionResult<object>> CreateTransaction([FromBody] TransactionRequest request)
    {
        try
        {
            var transaction = new Transaction(
                request.Sender,
                request.Recipient,
                request.Amount
            );

            blockchain.AddTransaction(transaction);

            // Notify all clients
            await hubContext.Clients.All.SendAsync("TransactionAdded", new
            {
                sender = transaction.Sender,
                recipient = transaction.Recipient,
                amount = transaction.Amount,
                timestamp = transaction.Timestamp
            });

            return Ok(new
            {
                message = "Transaction added to pending pool",
                transaction = new
                {
                    id = transaction.TransactionId,
                    sender = transaction.Sender,
                    recipient = transaction.Recipient,
                    amount = transaction.Amount
                }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("pending")]
    public ActionResult<object> GetPendingTransactions()
    {
        return Ok(new
        {
            count = blockchain.PendingTransactions.Count,
            transactions = blockchain.PendingTransactions
        });
    }

    [HttpGet("history/{address}")]
    public ActionResult<object> GetTransactionHistory(string address)
    {
        var history = new List<object>();

        foreach (var block in blockchain.Chain)
        {
            foreach (var tx in block.Transactions)
            {
                if (tx.Sender == address || tx.Recipient == address)
                {
                    history.Add(new
                    {
                        blockIndex = block.Index,
                        transactionId = tx.TransactionId,
                        sender = tx.Sender,
                        recipient = tx.Recipient,
                        amount = tx.Amount,
                        timestamp = tx.Timestamp
                    });
                }
            }
        }

        return Ok(history);
    }
}

public class TransactionRequest
{
    public string Sender { get; set; }
    public string Recipient { get; set; }
    public double Amount { get; set; }
}
```

#### Step 4: Mining Controller

```csharp
// Controllers/MiningController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

[ApiController]
[Route("api/[controller]")]
public class MiningController : ControllerBase
{
    private static Blockchain blockchain;
    private readonly IHubContext<BlockchainHub> hubContext;
    private static bool isMining = false;

    public MiningController(IHubContext<BlockchainHub> hubContext)
    {
        this.hubContext = hubContext;
        blockchain = BlockchainService.Instance;
    }

    [HttpPost("mine")]
    public async Task<ActionResult<object>> MineBlock([FromBody] MineRequest request)
    {
        if (isMining)
            return BadRequest(new { error = "Mining already in progress" });

        try
        {
            isMining = true;

            await hubContext.Clients.All.SendAsync("MiningStarted", new
            {
                minerAddress = request.MinerAddress,
                pendingTransactions = blockchain.PendingTransactions.Count
            });

            // Mine in background
            await Task.Run(() =>
            {
                blockchain.MinePendingTransactions(request.MinerAddress);
            });

            var latestBlock = blockchain.GetLatestBlock();

            await hubContext.Clients.All.SendAsync("BlockMined", new
            {
                index = latestBlock.Index,
                hash = latestBlock.Hash,
                nonce = latestBlock.Nonce,
                transactions = latestBlock.Transactions.Count,
                minerAddress = request.MinerAddress
            });

            isMining = false;

            return Ok(new
            {
                message = "Block mined successfully",
                block = new
                {
                    index = latestBlock.Index,
                    hash = latestBlock.Hash,
                    nonce = latestBlock.Nonce,
                    transactions = latestBlock.Transactions.Count
                }
            });
        }
        catch (Exception ex)
        {
            isMining = false;
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("status")]
    public ActionResult<object> GetMiningStatus()
    {
        return Ok(new
        {
            isMining,
            difficulty = blockchain.Difficulty,
            pendingTransactions = blockchain.PendingTransactions.Count
        });
    }
}

public class MineRequest
{
    public string MinerAddress { get; set; }
}
```

#### Step 5: SignalR Hub for Real-time Updates

```csharp
// Hubs/BlockchainHub.cs
using Microsoft.AspNetCore.SignalR;

public class BlockchainHub : Hub
{
    public async Task SubscribeToBlockchain()
    {
        await Clients.Caller.SendAsync("Subscribed", new
        {
            message = "Successfully subscribed to blockchain updates"
        });
    }

    public async Task RequestChainUpdate()
    {
        var blockchain = BlockchainService.Instance;
        await Clients.Caller.SendAsync("ChainUpdate", new
        {
            length = blockchain.Chain.Count,
            chain = blockchain.Chain
        });
    }
}
```

#### Step 6: Blockchain Service (Singleton)

```csharp
// Services/BlockchainService.cs
public class BlockchainService
{
    private static Blockchain _instance;
    private static readonly object _lock = new object();

    public static Blockchain Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new Blockchain(difficulty: 3, miningReward: 100);
                    }
                }
            }
            return _instance;
        }
    }
}
```

#### Step 7: Program.cs Configuration

```csharp
// Program.cs
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseStaticFiles();
app.MapControllers();
app.MapHub<BlockchainHub>("/blockchain-hub");

app.Run();
```

### Frontend Implementation

#### Step 8: HTML Interface

```html
<!-- wwwroot/index.html -->
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Blockchain Demo</title>
    <link rel="stylesheet" href="styles.css">
    <script src="https://cdn.jsdelivr.net/npm/@microsoft/signalr@latest/dist/browser/signalr.min.js"></script>
</head>
<body>
    <div class="container">
        <header>
            <h1>🔗 Blockchain Live Demo</h1>
            <div class="stats-bar">
                <div class="stat">
                    <span class="label">Blocks:</span>
                    <span id="blockCount">0</span>
                </div>
                <div class="stat">
                    <span class="label">Difficulty:</span>
                    <span id="difficulty">0</span>
                </div>
                <div class="stat">
                    <span class="label">Pending TX:</span>
                    <span id="pendingTx">0</span>
                </div>
                <div class="stat">
                    <span class="label">Status:</span>
                    <span id="chainStatus" class="status-badge">Valid</span>
                </div>
            </div>
        </header>

        <div class="main-content">
            <!-- Transaction Form -->
            <section class="card">
                <h2>📝 Create Transaction</h2>
                <form id="transactionForm">
                    <div class="form-group">
                        <label>Sender Address:</label>
                        <input type="text" id="sender" placeholder="Alice" required>
                    </div>
                    <div class="form-group">
                        <label>Recipient Address:</label>
                        <input type="text" id="recipient" placeholder="Bob" required>
                    </div>
                    <div class="form-group">
                        <label>Amount:</label>
                        <input type="number" id="amount" placeholder="100" step="0.01" required>
                    </div>
                    <button type="submit" class="btn btn-primary">Send Transaction</button>
                </form>
                <div id="transactionResult" class="result-message"></div>
            </section>

            <!-- Mining Controls -->
            <section class="card">
                <h2>⛏️ Mining</h2>
                <div class="form-group">
                    <label>Miner Address:</label>
                    <input type="text" id="minerAddress" placeholder="Miner1" value="Miner1">
                </div>
                <button id="mineBtn" class="btn btn-success">Mine Block</button>
                <div id="miningStatus" class="mining-status"></div>
                <div class="mining-info">
                    <p>💡 Mining will process pending transactions and create a new block</p>
                </div>
            </section>

            <!-- Balance Checker -->
            <section class="card">
                <h2>💰 Check Balance</h2>
                <div class="form-group">
                    <label>Address:</label>
                    <input type="text" id="balanceAddress" placeholder="Alice">
                </div>
                <button id="checkBalanceBtn" class="btn btn-info">Check Balance</button>
                <div id="balanceResult" class="result-message"></div>
            </section>
        </div>

        <!-- Blockchain Visualization -->
        <section class="blockchain-section">
            <h2>🔗 Blockchain</h2>
            <button id="refreshBtn" class="btn btn-secondary">🔄 Refresh</button>
            <div id="blockchain" class="blockchain-container"></div>
        </section>

        <!-- Pending Transactions -->
        <section class="card">
            <h2>⏳ Pending Transactions</h2>
            <div id="pendingTransactions" class="transaction-list"></div>
        </section>
    </div>

    <div id="notification" class="notification hidden"></div>

    <script src="app.js"></script>
</body>
</html>
```

#### Step 9: CSS Styling

```css
/* wwwroot/styles.css */
* {
    margin: 0;
    padding: 0;
    box-sizing: border-box;
}

body {
    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    min-height: 100vh;
    padding: 20px;
    color: #333;
}

.container {
    max-width: 1400px;
    margin: 0 auto;
}

header {
    background: white;
    border-radius: 12px;
    padding: 30px;
    margin-bottom: 30px;
    box-shadow: 0 10px 30px rgba(0, 0, 0, 0.2);
}

h1 {
    color: #667eea;
    margin-bottom: 20px;
    font-size: 2.5em;
}

.stats-bar {
    display: flex;
    gap: 30px;
    flex-wrap: wrap;
}

.stat {
    display: flex;
    flex-direction: column;
    gap: 5px;
}

.stat .label {
    font-size: 0.9em;
    color: #666;
    font-weight: 500;
}

.stat span:last-child {
    font-size: 1.5em;
    font-weight: bold;
    color: #667eea;
}

.status-badge {
    display: inline-block;
    padding: 5px 15px;
    border-radius: 20px;
    font-size: 0.9em !important;
    font-weight: bold !important;
}

.status-badge.valid {
    background: #10b981;
    color: white;
}

.status-badge.invalid {
    background: #ef4444;
    color: white;
}

.main-content {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(350px, 1fr));
    gap: 20px;
    margin-bottom: 30px;
}

.card {
    background: white;
    border-radius: 12px;
    padding: 25px;
    box-shadow: 0 5px 15px rgba(0, 0, 0, 0.1);
}

.card h2 {
    color: #667eea;
    margin-bottom: 20px;
    font-size: 1.5em;
}

.form-group {
    margin-bottom: 15px;
}

.form-group label {
    display: block;
    margin-bottom: 5px;
    font-weight: 500;
    color: #555;
}

.form-group input {
    width: 100%;
    padding: 12px;
    border: 2px solid #e0e0e0;
    border-radius: 8px;
    font-size: 1em;
    transition: border-color 0.3s;
}

.form-group input:focus {
    outline: none;
    border-color: #667eea;
}

.btn {
    padding: 12px 24px;
    border: none;
    border-radius: 8px;
    font-size: 1em;
    font-weight: 600;
    cursor: pointer;
    transition: all 0.3s;
    width: 100%;
}

.btn-primary {
    background: #667eea;
    color: white;
}

.btn-primary:hover {
    background: #5568d3;
    transform: translateY(-2px);
    box-shadow: 0 5px 15px rgba(102, 126, 234, 0.4);
}

.btn-success {
    background: #10b981;
    color: white;
}

.btn-success:hover {
    background: #059669;
    transform: translateY(-2px);
}

.btn-success:disabled {
    background: #9ca3af;
    cursor: not-allowed;
    transform: none;
}

.btn-info {
    background: #3b82f6;
    color: white;
}

.btn-info:hover {
    background: #2563eb;
}

.btn-secondary {
    background: #6b7280;
    color: white;
    width: auto;
    margin-bottom: 15px;
}

.result-message {
    margin-top: 15px;
    padding: 12px;
    border-radius: 8px;
    display: none;
}

.result-message.success {
    background: #d1fae5;
    color: #065f46;
    display: block;
}

.result-message.error {
    background: #fee2e2;
    color: #991b1b;
    display: block;
}

.mining-status {
    margin-top: 15px;
    padding: 12px;
    border-radius: 8px;
    background: #f3f4f6;
    display: none;
}

.mining-status.active {
    display: block;
    background: #fef3c7;
    color: #92400e;
}

.mining-info {
    margin-top: 15px;
    padding: 12px;
    background: #eff6ff;
    border-left: 4px solid #3b82f6;
    border-radius: 4px;
    font-size: 0.9em;
    color: #1e40af;
}

.blockchain-section {
    background: white;
    border-radius: 12px;
    padding: 25px;
    margin-bottom: 30px;
    box-shadow: 0 5px 15px rgba(0, 0, 0, 0.1);
}

.blockchain-container {
    display: flex;
    gap: 20px;
    overflow-x: auto;
    padding: 20px 0;
}

.block {
    min-width: 300px;
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    color: white;
    padding: 20px;
    border-radius: 12px;
    box-shadow: 0 5px 15px rgba(0, 0, 0, 0.2);
    position: relative;
}

.block::after {
    content: '→';
    position: absolute;
    right: -30px;
    top: 50%;
    transform: translateY(-50%);
    font-size: 2em;
    color: #667eea;
}

.block:last-child::after {
    content: '';
}

.block-header {
    border-bottom: 2px solid rgba(255, 255, 255, 0.3);
    padding-bottom: 10px;
    margin-bottom: 15px;
}

.block-index {
    font-size: 1.5em;
    font-weight: bold;
}

.block-detail {
    margin: 8px 0;
    font-size: 0.9em;
}

.block-detail strong {
    display: inline-block;
    width: 80px;
}

.hash {
    font-family: 'Courier New', monospace;
    font-size: 0.8em;
    word-break: break-all;
    background: rgba(0, 0, 0, 0.2);
    padding: 5px;
    border-radius: 4px;
    margin-top: 5px;
}

.transaction-list {
    max-height: 300px;
    overflow-y: auto;
}

.transaction-item {
    padding: 12px;
    background: #f9fafb;
    border-left: 4px solid #667eea;
    border-radius: 4px;
    margin-bottom: 10px;
}

.transaction-item:last-child {
    margin-bottom: 0;
}

.notification {
    position: fixed;
    top: 20px;
    right: 20px;
    padding: 15px 25px;
    background: white;
    border-radius: 8px;
    box-shadow: 0 10px 30px rgba(0, 0, 0, 0.3);
    z-index: 1000;
    animation: slideIn 0.3s ease-out;
}

.notification.hidden {
    display: none;
}

.notification.success {
    border-left: 4px solid #10b981;
}

.notification.error {
    border-left: 4px solid #ef4444;
}

.notification.info {
    border-left: 4px solid #3b82f6;
}

@keyframes slideIn {
    from {
        transform: translateX(400px);
        opacity: 0;
    }
    to {
        transform: translateX(0);
        opacity: 1;
    }
}

@media (max-width: 768px) {
    .main-content {
        grid-template-columns: 1fr;
    }
    
    .blockchain-container {
        flex-direction: column;
    }
    
    .block::after {
        content: '↓';
        right: 50%;
        top: auto;
        bottom: -30px;
        transform: translateX(50%);
    }
}
```

#### Step 10: JavaScript Application

```javascript
// wwwroot/app.js
const API_BASE = 'http://localhost:5000/api';
let connection;

// Initialize SignalR connection
async function initializeSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("http://localhost:5000/blockchain-hub")
        .withAutomaticReconnect()
        .build();

    connection.on("TransactionAdded", (data) => {
        showNotification(`Transaction added: ${data.sender} → ${data.recipient} (${data.amount})`, 'info');
        updateStats();
        loadPendingTransactions();
    });

    connection.on("BlockMined", (data) => {
        showNotification(`Block #${data.index} mined! Hash: ${data.hash.substring(0, 10)}...`, 'success');
        updateStats();
        loadBlockchain();
        loadPendingTransactions();
        document.getElementById('miningStatus').classList.remove('active');
        document.getElementById('mineBtn').disabled = false;
    });

    connection.on("MiningStarted", (data) => {
        const status = document.getElementById('miningStatus');
        status.textContent = `Mining in progress... (${data.pendingTransactions} transactions)`;
        status.classList.add('active');
        document.getElementById('mineBtn').disabled = true;
    });

    try {
        await connection.start();
        console.log("SignalR Connected");
    } catch (err) {
        console.error("SignalR Connection Error:", err);
        setTimeout(initializeSignalR, 5000);
    }
}

// Show notification
function showNotification(message, type = 'info') {
    const notification = document.getElementById('notification');
    notification.textContent = message;
    notification.className = `notification ${type}`;
    
    setTimeout(() => {
        notification.classList.add('hidden');
    }, 5000);
}

// Update statistics
async function updateStats() {
    try {
        const response = await fetch(`${API_BASE}/blockchain/stats`);
        const data = await response.json();
        
        document.getElementById('blockCount').textContent = data.totalBlocks;
        document.getElementById('difficulty').textContent = data.difficulty;
        document.getElementById('pendingTx').textContent = data.pendingTransactions;
        
        const statusBadge = document.getElementById('chainStatus');
        statusBadge.textContent = data.isValid ? 'Valid' : 'Invalid';
        statusBadge.className = `status-badge ${data.isValid ? 'valid' : 'invalid'}`;
    } catch (error) {
        console.error('Error updating stats:', error);
    }
}

// Load and display blockchain
async function loadBlockchain() {
    try {
        const response = await fetch(`${API_BASE}/blockchain/chain`);
        const data = await response.json();
        
        const container = document.getElementById('blockchain');
        container.innerHTML = '';
        
        data.chain.forEach(block => {
            const blockDiv = document.createElement('div');
            blockDiv.className = 'block';
            blockDiv.innerHTML = `
                <div class="block-header">
                    <div class="block-index">Block #${block.index}</div>
                </div>
                <div class="block-detail">
                    <strong>Timestamp:</strong><br>
                    ${new Date(block.timestamp).toLocaleString()}
                </div>
                <div class="block-detail">
                    <strong>Nonce:</strong> ${block.nonce}
                </div>
                <div class="block-detail">
                    <strong>Transactions:</strong> ${block.transactions.length}
                </div>
                <div class="block-detail">
                    <strong>Hash:</strong>
                    <div class="hash">${block.hash}</div>
                </div>
                <div class="block-detail">
                    <strong>Prev Hash:</strong>
                    <div class="hash">${block.previousHash}</div>
                </div>
            `;
            container.appendChild(blockDiv);
        });
    } catch (error) {
        console.error('Error loading blockchain:', error);
        showNotification('Error loading blockchain', 'error');
    }
}

// Load pending transactions
async function loadPendingTransactions() {
    try {
        const response = await fetch(`${API_BASE}/transaction/pending`);
        const data = await response.json();
        
        const container = document.getElementById('pendingTransactions');
        
        if (data.count === 0) {
            container.innerHTML = '<p style="color: #6b7280;">No pending transactions</p>';
            return;
        }
        
        container.innerHTML = data.transactions.map(tx => `
            <div class="transaction-item">
                <div><strong>${tx.sender}</strong> → <strong>${tx.recipient}</strong></div>
                <div>Amount: <strong>${tx.amount}</strong></div>
                <div style="font-size: 0.9em; color: #6b7280;">
                    ${new Date(tx.timestamp).toLocaleString()}
                </div>
            </div>
        `).join('');
    } catch (error) {
        console.error('Error loading pending transactions:', error);
    }
}

// Create transaction
document.getElementById('transactionForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    
    const sender = document.getElementById('sender').value;
    const recipient = document.getElementById('recipient').value;
    const amount = parseFloat(document.getElementById('amount').value);
    
    try {
        const response = await fetch(`${API_BASE}/transaction`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ sender, recipient, amount })
        });
        
        const result = await response.json();
        const resultDiv = document.getElementById('transactionResult');
        
        if (response.ok) {
            resultDiv.className = 'result-message success';
            resultDiv.textContent = result.message;
            document.getElementById('transactionForm').reset();
        } else {
            resultDiv.className = 'result-message error';
            resultDiv.textContent = result.error || 'Transaction failed';
        }
    } catch (error) {
        const resultDiv = document.getElementById('transactionResult');
        resultDiv.className = 'result-message error';
        resultDiv.textContent = 'Error creating transaction';
    }
});

// Mine block
document.getElementById('mineBtn').addEventListener('click', async () => {
    const minerAddress = document.getElementById('minerAddress').value;
    
    try {
        const response = await fetch(`${API_BASE}/mining/mine`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ minerAddress })
        });
        
        if (!response.ok) {
            const error = await response.json();
            showNotification(error.error || 'Mining failed', 'error');
        }
    } catch (error) {
        showNotification('Error starting mining', 'error');
    }
});

// Check balance
document.getElementById('checkBalanceBtn').addEventListener('click', async () => {
    const address = document.getElementById('balanceAddress').value;
    
    try {
        const response = await fetch(`${API_BASE}/blockchain/balance/${encodeURIComponent(address)}`);
        const data = await response.json();
        
        const resultDiv = document.getElementById('balanceResult');
        resultDiv.className = 'result-message success';
        resultDiv.textContent = `Balance for ${data.address}: ${data.balance}`;
    } catch (error) {
        const resultDiv = document.getElementById('balanceResult');
        resultDiv.className = 'result-message error';
        resultDiv.textContent = 'Error checking balance';
    }
});

// Refresh blockchain
document.getElementById('refreshBtn').addEventListener('click', () => {
    loadBlockchain();
    updateStats();
    loadPendingTransactions();
});

// Initialize on load
document.addEventListener('DOMContentLoaded', async () => {
    await initializeSignalR();
    await updateStats();
    await loadBlockchain();
    await loadPendingTransactions();
    
    // Auto-refresh every 5 seconds
    setInterval(updateStats, 5000);
});
```

### Running the Demo

#### Step 11: Launch Instructions

```bash
# 1. Navigate to API project
cd BlockchainDemo.API

# 2. Run the application
dotnet run

# 3. Open browser to:
# - API: http://localhost:5000/swagger (for API docs)
# - Frontend: http://localhost:5000/index.html

# The demo should now be live!
```

### Demo Features

1. **Real-time Updates**: WebSocket connection shows live blockchain changes
2. **Visual Blockchain**: See blocks linked together visually
3. **Transaction Creation**: Create transactions via web form
4. **Mining Simulation**: Mine blocks and watch difficulty adjust
5. **Balance Checking**: Query any address balance
6. **Statistics Dashboard**: Live stats on blocks, difficulty, pending transactions
7. **Chain Validation**: Real-time validation status

### Optional Enhancements

#### Add Multi-Node Visualization

```javascript
// Add network visualization with multiple nodes
function visualizeNetwork() {
    // Use D3.js or similar to show:
    // - Multiple nodes
    // - Peer connections
    // - Transaction propagation
    // - Block propagation
}
```

#### Add Wallet Management UI

```html
<section class="card">
    <h2>👛 Wallet Manager</h2>
    <button id="createWalletBtn">Create New Wallet</button>
    <div id="walletList"></div>
</section>
```

#### Add Mining Animation

```css
@keyframes mining {
    0%, 100% { transform: rotate(-5deg); }
    50% { transform: rotate(5deg); }
}

.mining .block {
    animation: mining 0.5s infinite;
}
```

---

## Testing Strategy

### Unit Tests

```csharp
// BlockchainTests.cs
using Xunit;

public class BlockchainTests
{
    [Fact]
    public void GenesisBlock_ShouldBeValid()
    {
        var blockchain = new Blockchain();
        Assert.True(blockchain.IsChainValid());
    }

    [Fact]
    public void AddTransaction_ShouldIncreaseBalance()
    {
        var blockchain = new Blockchain();
        var tx = new Transaction("Alice", "Bob", 50);
        blockchain.AddTransaction(tx);
        blockchain.MinePendingTransactions("Miner1");
        
        Assert.Equal(-50, blockchain.GetBalance("Alice"));
        Assert.Equal(50, blockchain.GetBalance("Bob"));
    }

    [Fact]
    public void TamperedBlock_ShouldInvalidateChain()
    {
        var blockchain = new Blockchain();
        blockchain.MinePendingTransactions("Miner1");
        
        // Tamper with block
        blockchain.Chain[1].Transactions[0].Amount = 1000;
        
        Assert.False(blockchain.IsChainValid());
    }
}
```

### Integration Tests

```csharp
// API Integration Tests
public class BlockchainAPITests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public BlockchainAPITests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetChain_ReturnsBlockchain()
    {
        var response = await _client.GetAsync("/api/blockchain/chain");
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("chain", content);
    }
}
```

---

## Performance Optimization Tips

1. **Caching**: Cache blockchain state for read-heavy operations
2. **Async Mining**: Run mining in background threads
3. **Pagination**: Paginate blockchain display for large chains
4. **Compression**: Compress historical blocks
5. **Database**: Consider persisting to database (SQLite, PostgreSQL)

---

## Deployment Options

### Docker Deployment

```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["BlockchainDemo.API/BlockchainDemo.API.csproj", "BlockchainDemo.API/"]
RUN dotnet restore
COPY . .
WORKDIR "/src/BlockchainDemo.API"
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BlockchainDemo.API.dll"]
```

```bash
# Build and run
docker build -t blockchain-demo .
docker run -p 5000:80 blockchain-demo
```

### Cloud Deployment

```bash
# Azure App Service
az webapp up --name my-blockchain-demo --runtime "DOTNETCORE:8.0"

# AWS Elastic Beanstalk
eb init -p "64bit Amazon Linux 2 v2.x.x running .NET Core" my-blockchain
eb create blockchain-demo-env
```

---

## Security Considerations

1. **API Rate Limiting**: Prevent mining spam
2. **Input Validation**: Sanitize all user inputs
3. **CORS**: Configure properly for production
4. **Authentication**: Add JWT for production use
5. **HTTPS**: Always use HTTPS in production

---

## Next Steps

After implementing the live demo:

1. **Add more features** from the extension specs
2. **Implement P2P networking** between browser tabs
3. **Add smart contract execution** visualization
4. **Create mobile app** version
5. **Deploy to cloud** for public access

---

## Resources

- [ASP.NET Core Documentation](https://docs.microsoft.com/en-us/aspnet/core)
- [SignalR Documentation](https://docs.microsoft.com/en-us/aspnet/core/signalr)
- [Blockchain Fundamentals](https://bitcoin.org/bitcoin.pdf)
- [Ethereum Whitepaper](https://ethereum.org/en/whitepaper/)

---

## Conclusion

This specification provides:
- ✅ Complete implementation details for 10 extensions
- ✅ Code examples for each feature
- ✅ Live demo with real-time updates
- ✅ Frontend + Backend integration
- ✅ Deployment instructions
- ✅ Testing strategies

You now have everything needed to build a production-quality blockchain demo!