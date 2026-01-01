using Models;
using System;

Console.WriteLine("Creating the blockchain");

BlockChain MyBlockchain = new BlockChain(difficulty: 4, miningReward: 50.0);

Console.WriteLine("Creating transactions list");
List<string> ExampleNames = new List<string>()
{
    "Alice",
    "Bob",
    "Charlie",
    "Diana",
    "Eve"
};

List<double> ExampleAmounts = new List<double>()
{
    50.50,
    75.50,
    100.00,
    200.00,
    150.00
};

List<Transaction> TransactionsList = new List<Transaction>();

for (int i = 0; i<=10; i++)
{
    Random rand = new Random();
    string sender = ExampleNames[rand.Next(ExampleNames.Count)];
    string recipient = ExampleNames[rand.Next(ExampleNames.Count)];
    double amount = ExampleAmounts[rand.Next(ExampleAmounts.Count)];

    Transaction transaction = new Transaction(sender, recipient, amount);
    TransactionsList.Add(transaction);
    Console.WriteLine(transaction.ToString());
}

Console.WriteLine("Adding transactions to blockchain and mining blocks");
foreach (var tx in TransactionsList)
{
    MyBlockchain.AddTransaction(tx);
    
    // Mine a block for every 2 transactions
    if (MyBlockchain.PendingTransactions.Count >= 2)
    {
        MyBlockchain.MinePendingTransactions("Miner1");
    }
}

// MyBlockchain.PrintChain();

// Check balances
Console.WriteLine("=== BALANCES ===");
Console.WriteLine($"Alice: {MyBlockchain.GetBalance("Alice")}");
Console.WriteLine($"Bob: {MyBlockchain.GetBalance("Bob")}");
Console.WriteLine($"Charlie: {MyBlockchain.GetBalance("Charlie")}");
Console.WriteLine($"Miner1: {MyBlockchain.GetBalance("Miner1")}");

// Validate chain
Console.WriteLine($"\nIs blockchain valid? {MyBlockchain.IsChainValid()}");

// Try to tamper with the blockchain
Console.WriteLine("\n=== ATTEMPTING TO TAMPER ===");
Console.WriteLine("Changing transaction amount in block 1...");
MyBlockchain.Chain[1].Transactions[0].Amount = 1000;
Console.WriteLine($"Is blockchain valid? {MyBlockchain.IsChainValid()}");