# Phase 4: Blazor Web UI

## Overview

Create a web-based user interface using Blazor Server that allows users to interact with their local blockchain node through a browser.

> **💡 Why a Web UI?** See [WHY-THESE-FEATURES.md](WHY-THESE-FEATURES.md#user-experience-features) for explanations of why a web UI makes blockchain accessible, educational, and demo-ready compared to command-line interfaces.

## Goals

- Set up Blazor Server infrastructure
- Create interactive UI components
- Display blockchain data in real-time
- Enable transaction submission and mining
- Show peer network status

## Step 1: Update Project for Blazor

### 1.1 Update Program.cs

Add Blazor services before `var app = builder.Build();`:

```csharp
// Add Razor Pages and Blazor Server
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
```

Add Blazor middleware after `app.UseRouting();`:

```csharp
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
```

## Step 2: Create Blazor Infrastructure

### 2.1 Create Required Folders

```bash
mkdir -p Pages/Shared
mkdir -p Pages/Components
mkdir -p wwwroot/css
```

### 2.2 Create _Host.cshtml

**File:** `Pages/_Host.cshtml`

```html
@page "/"
@using Microsoft.AspNetCore.Components.Web
@namespace blockchain_example_project.Pages
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers

<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Blockchain Node</title>
    <base href="~/" />
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css" rel="stylesheet">
    <link href="css/app.css" rel="stylesheet" />
</head>
<body>
    <component type="typeof(App)" render-mode="ServerPrerendered" />

    <script src="_framework/blazor.server.js"></script>
</body>
</html>
```

### 2.3 Create App.razor

**File:** `Pages/App.razor`

```razor
<Router AppAssembly="@typeof(App).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)" />
    </Found>
    <NotFound>
        <h1>404 - Page not found</h1>
    </NotFound>
</Router>
```

### 2.4 Create _Imports.razor

**File:** `Pages/_Imports.razor`

```razor
@using System.Net.Http
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.JSInterop
@using blockchain_example_project.Pages
@using blockchain_example_project.Pages.Shared
@using blockchain_example_project.Pages.Components
@using Models
@using Services
@using Network
```

### 2.5 Create MainLayout.razor

**File:** `Pages/Shared/MainLayout.razor`

```razor
@inherits LayoutComponentBase
@inject P2PNode P2PNode
@inject PeerManager PeerManager

<div class="page">
    <header class="navbar navbar-dark bg-dark">
        <div class="container-fluid">
            <span class="navbar-brand">Blockchain Node - @P2PNode.NodeId[..8]...</span>
            <span class="badge bg-success">@PeerCount Peers Connected</span>
        </div>
    </header>

    <main class="container-fluid mt-4">
        @Body
    </main>
</div>

@code {
    private int PeerCount => PeerManager.GetConnectedPeers().Count;
}
```

## Step 3: Create Main Dashboard

### 3.1 Create Index.razor

**File:** `Pages/Index.razor`

```razor
@page "/"
@inject BlockchainNodeService NodeService
@inject P2PNode P2PNode
@inject PeerManager PeerManager
@inject ConsensusManager Consensus
@implements IDisposable

<div class="row">
    <!-- Node Status Card -->
    <div class="col-md-12 mb-4">
        <div class="card">
            <div class="card-header bg-primary text-white">
                <h5>Node Status</h5>
            </div>
            <div class="card-body">
                <div class="row">
                    <div class="col-md-3">
                        <strong>Chain Length:</strong> @chainLength blocks
                    </div>
                    <div class="col-md-3">
                        <strong>Difficulty:</strong> @difficulty
                    </div>
                    <div class="col-md-3">
                        <strong>Mining Reward:</strong> @miningReward
                    </div>
                    <div class="col-md-3">
                        <strong>Chain Valid:</strong>
                        @if (isValid)
                        {
                            <span class="badge bg-success">Valid</span>
                        }
                        else
                        {
                            <span class="badge bg-danger">Invalid</span>
                        }
                    </div>
                </div>
            </div>
        </div>
    </div>
</div>

<div class="row">
    <!-- Transaction Form -->
    <div class="col-md-6 mb-4">
        <div class="card">
            <div class="card-header bg-info text-white">
                <h5>Submit Transaction</h5>
            </div>
            <div class="card-body">
                <EditForm Model="@transactionModel" OnValidSubmit="@SubmitTransaction">
                    <div class="mb-3">
                        <label class="form-label">Sender</label>
                        <InputText class="form-control" @bind-Value="transactionModel.Sender" />
                    </div>
                    <div class="mb-3">
                        <label class="form-label">Recipient</label>
                        <InputText class="form-control" @bind-Value="transactionModel.Recipient" />
                    </div>
                    <div class="mb-3">
                        <label class="form-label">Amount</label>
                        <InputNumber class="form-control" @bind-Value="transactionModel.Amount" />
                    </div>
                    <button type="submit" class="btn btn-primary" disabled="@isSubmitting">
                        @(isSubmitting ? "Broadcasting..." : "Submit & Broadcast")
                    </button>
                </EditForm>
                @if (!string.IsNullOrEmpty(transactionMessage))
                {
                    <div class="alert alert-success mt-3">@transactionMessage</div>
                }
            </div>
        </div>
    </div>

    <!-- Mining Section -->
    <div class="col-md-6 mb-4">
        <div class="card">
            <div class="card-header bg-warning text-dark">
                <h5>Mine Block</h5>
            </div>
            <div class="card-body">
                <div class="mb-3">
                    <label class="form-label">Miner Address</label>
                    <input type="text" class="form-control" @bind="minerAddress" />
                </div>
                <div class="mb-3">
                    <strong>Pending Transactions:</strong> @pendingCount
                </div>
                <button class="btn btn-warning" @onclick="MineBlock" disabled="@isMining">
                    @(isMining ? "Mining..." : "Mine Block")
                </button>
                @if (!string.IsNullOrEmpty(miningMessage))
                {
                    <div class="alert alert-info mt-3">@miningMessage</div>
                }
            </div>
        </div>
    </div>
</div>

<div class="row">
    <!-- Balance Checker -->
    <div class="col-md-6 mb-4">
        <div class="card">
            <div class="card-header bg-success text-white">
                <h5>Check Balance</h5>
            </div>
            <div class="card-body">
                <div class="mb-3">
                    <label class="form-label">Address</label>
                    <input type="text" class="form-control" @bind="balanceAddress" />
                </div>
                <button class="btn btn-success" @onclick="CheckBalance">Check Balance</button>
                @if (balance.HasValue)
                {
                    <div class="alert alert-success mt-3">
                        Balance: <strong>@balance.Value</strong>
                    </div>
                }
            </div>
        </div>
    </div>

    <!-- Peer Management -->
    <div class="col-md-6 mb-4">
        <div class="card">
            <div class="card-header bg-secondary text-white">
                <h5>Connected Peers (@peers.Count)</h5>
            </div>
            <div class="card-body">
                <div class="mb-3">
                    <label class="form-label">Peer Address (host:port)</label>
                    <input type="text" class="form-control" @bind="newPeerAddress" placeholder="localhost:5002" />
                </div>
                <button class="btn btn-secondary" @onclick="ConnectToPeer">Connect to Peer</button>

                <div class="mt-3">
                    @foreach (var peer in peers)
                    {
                        <div class="peer-item">
                            <strong>@peer.NodeId[..8]...</strong> (@peer.Address)
                            <span class="badge @(peer.IsConnected ? "bg-success" : "bg-danger")">
                                @(peer.IsConnected ? "Connected" : "Disconnected")
                            </span>
                        </div>
                    }
                </div>
            </div>
        </div>
    </div>
</div>

<div class="row">
    <!-- Blockchain Viewer -->
    <div class="col-md-12">
        <div class="card">
            <div class="card-header bg-dark text-white">
                <h5>Blockchain (@chainLength blocks)</h5>
            </div>
            <div class="card-body">
                @foreach (var block in blocks.OrderByDescending(b => b.Index))
                {
                    <div class="block-card">
                        <div class="block-header">
                            <strong>Block #@block.Index</strong>
                            <small class="text-muted">@block.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")</small>
                        </div>
                        <div class="block-body">
                            <div><strong>Hash:</strong> <code>@block.Hash[..16]...</code></div>
                            <div><strong>Previous:</strong> <code>@block.PreviousHash[..16]...</code></div>
                            <div><strong>Nonce:</strong> @block.Nonce</div>
                            <div><strong>Transactions:</strong> @block.Transactions.Count</div>

                            @if (block.Transactions.Any())
                            {
                                <table class="table table-sm mt-2">
                                    <thead>
                                        <tr>
                                            <th>Sender</th>
                                            <th>Recipient</th>
                                            <th>Amount</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        @foreach (var tx in block.Transactions)
                                        {
                                            <tr>
                                                <td>@tx.Sender</td>
                                                <td>@tx.Recipient</td>
                                                <td>@tx.Amount</td>
                                            </tr>
                                        }
                                    </tbody>
                                </table>
                            }
                        </div>
                    </div>
                }
            </div>
        </div>
    </div>
</div>

@code {
    private Timer? refreshTimer;
    private int chainLength;
    private int difficulty;
    private double miningReward;
    private bool isValid;
    private int pendingCount;
    private List<Block> blocks = new();
    private List<PeerInfo> peers = new();

    private TransactionModel transactionModel = new();
    private string transactionMessage = "";
    private bool isSubmitting = false;

    private string minerAddress = "Miner1";
    private string miningMessage = "";
    private bool isMining = false;

    private string balanceAddress = "";
    private double? balance;

    private string newPeerAddress = "";

    protected override void OnInitialized()
    {
        RefreshData();
        refreshTimer = new Timer(_ =>
        {
            InvokeAsync(() =>
            {
                RefreshData();
                StateHasChanged();
            });
        }, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
    }

    private void RefreshData()
    {
        var chain = NodeService.GetChain();
        chainLength = chain.Chain.Count;
        difficulty = chain.Difficulty;
        miningReward = chain.MiningReward;
        isValid = NodeService.IsChainValid();
        pendingCount = NodeService.GetPendingTransactions().Count;
        blocks = chain.Chain.ToList();
        peers = PeerManager.GetAllPeers();
    }

    private async Task SubmitTransaction()
    {
        isSubmitting = true;
        try
        {
            var tx = new Transaction(transactionModel.Sender, transactionModel.Recipient, transactionModel.Amount);
            NodeService.AddTransaction(tx);
            await P2PNode.BroadcastTransaction(tx);

            transactionMessage = $"Transaction broadcasted to {peers.Count} peers!";
            transactionModel = new TransactionModel();
            RefreshData();
        }
        catch (Exception ex)
        {
            transactionMessage = $"Error: {ex.Message}";
        }
        finally
        {
            isSubmitting = false;
        }
    }

    private async Task MineBlock()
    {
        isMining = true;
        miningMessage = "Mining in progress...";
        StateHasChanged();

        try
        {
            await Task.Run(async () =>
            {
                var block = NodeService.MinePendingTransactions(minerAddress);
                await P2PNode.BroadcastBlock(block);
                miningMessage = $"Block mined! Hash: {block.Hash[..16]}... Broadcasted to {peers.Count} peers.";
            });

            RefreshData();
        }
        catch (Exception ex)
        {
            miningMessage = $"Error: {ex.Message}";
        }
        finally
        {
            isMining = false;
        }
    }

    private void CheckBalance()
    {
        balance = NodeService.GetBalance(balanceAddress);
    }

    private async Task ConnectToPeer()
    {
        if (!string.IsNullOrEmpty(newPeerAddress))
        {
            await P2PNode.ConnectToPeer(newPeerAddress);
            newPeerAddress = "";
            RefreshData();
        }
    }

    public void Dispose()
    {
        refreshTimer?.Dispose();
    }

    public class TransactionModel
    {
        public string Sender { get; set; } = "";
        public string Recipient { get; set; } = "";
        public double Amount { get; set; }
    }
}
```

## Step 4: Add Styling

### 4.1 Create app.css

**File:** `wwwroot/css/app.css`

```css
body {
    background-color: #f5f5f5;
}

.page {
    min-height: 100vh;
}

.block-card {
    border: 1px solid #dee2e6;
    border-radius: 0.25rem;
    padding: 1rem;
    margin-bottom: 1rem;
    background-color: white;
}

.block-header {
    display: flex;
    justify-content: space-between;
    margin-bottom: 0.5rem;
    padding-bottom: 0.5rem;
    border-bottom: 1px solid #dee2e6;
}

.block-body {
    font-size: 0.9rem;
}

.peer-item {
    padding: 0.5rem;
    border-bottom: 1px solid #dee2e6;
}

.peer-item:last-child {
    border-bottom: none;
}

code {
    background-color: #f8f9fa;
    padding: 0.2rem 0.4rem;
    border-radius: 0.25rem;
}
```

## Step 5: Testing

### 5.1 Run the Application

```bash
dotnet run
```

### 5.2 Access the UI

Open browser to: `http://localhost:5000`

You should see:
- Node status dashboard
- Transaction form
- Mining interface
- Balance checker
- Peer list
- Blockchain viewer with all blocks

### 5.3 Test Functionality

1. **Submit Transaction**: Fill form and click "Submit & Broadcast"
2. **Mine Block**: Enter miner address and click "Mine Block"
3. **Check Balance**: Enter address and check balance
4. **Connect Peer**: Start second node on port 5002, then connect
5. **Watch Auto-Update**: UI updates every 3 seconds automatically

## Completion Checklist

- [ ] Blazor services added to Program.cs
- [ ] _Host.cshtml created
- [ ] App.razor created
- [ ] MainLayout.razor created
- [ ] Index.razor created with all components
- [ ] app.css created
- [ ] Application builds successfully
- [ ] UI loads in browser
- [ ] Can submit transactions
- [ ] Can mine blocks
- [ ] Can check balances
- [ ] Can connect to peers
- [ ] Auto-refresh working
- [ ] Blockchain displays correctly

## Troubleshooting

### Issue: Blazor hub connection failed

**Solution:** Ensure `MapBlazorHub()` is called in Program.cs

### Issue: UI doesn't update

**Solution:** Check that `StateHasChanged()` is called after async operations

### Issue: Styles not loading

**Solution:** Verify `UseStaticFiles()` is added to Program.cs

## Next Steps

1. Test the UI with multiple nodes
2. Move on to **Phase 5: Docker Deployment** to containerize the application
3. The UI is now complete and fully functional!

## Summary

You now have:
- Interactive web-based UI
- Real-time blockchain visualization
- Transaction submission interface
- Mining controls
- Peer management
- Balance checking
- Auto-refreshing dashboard
- Complete user experience

Your blockchain node now has a professional web interface!
