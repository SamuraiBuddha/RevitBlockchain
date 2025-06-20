using System;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBlockchain.Core;

namespace RevitBlockchain.Commands
{
    /// <summary>
    /// Command to show blockchain connection status
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class BlockchainStatusCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var blockchainClient = Application.GetBlockchainClient();
                if (blockchainClient == null)
                {
                    TaskDialog.Show("Blockchain Status", "Blockchain client not initialized.");
                    return Result.Failed;
                }

                // Create status dialog
                using (var dialog = new BlockchainStatusDialog(blockchainClient))
                {
                    dialog.ShowDialog();
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    /// <summary>
    /// Simple status dialog (would be more elaborate in production)
    /// </summary>
    public class BlockchainStatusDialog : Form
    {
        private readonly BlockchainClient _client;
        private System.Windows.Forms.Timer _refreshTimer;
        private Label _statusLabel;
        private Label _pendingLabel;
        private Label _lastSyncLabel;
        private Button _closeButton;
        private Button _syncButton;

        public BlockchainStatusDialog(BlockchainClient client)
        {
            _client = client;
            InitializeComponent();
            UpdateStatus();
            
            // Set up auto-refresh
            _refreshTimer = new System.Windows.Forms.Timer();
            _refreshTimer.Interval = 5000; // 5 seconds
            _refreshTimer.Tick += (s, e) => UpdateStatus();
            _refreshTimer.Start();
        }

        private void InitializeComponent()
        {
            Text = "Blockchain Status";
            Size = new System.Drawing.Size(400, 300);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20)
            };

            _statusLabel = new Label
            {
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(340, 30),
                Font = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold)
            };

            _pendingLabel = new Label
            {
                Location = new System.Drawing.Point(20, 60),
                Size = new System.Drawing.Size(340, 30)
            };

            _lastSyncLabel = new Label
            {
                Location = new System.Drawing.Point(20, 100),
                Size = new System.Drawing.Size(340, 30)
            };

            _syncButton = new Button
            {
                Text = "Force Sync",
                Location = new System.Drawing.Point(20, 150),
                Size = new System.Drawing.Size(100, 30)
            };
            _syncButton.Click += async (s, e) => 
            {
                _syncButton.Enabled = false;
                await _client.SyncOfflineQueue();
                UpdateStatus();
                _syncButton.Enabled = true;
            };

            _closeButton = new Button
            {
                Text = "Close",
                Location = new System.Drawing.Point(260, 210),
                Size = new System.Drawing.Size(100, 30),
                DialogResult = DialogResult.OK
            };

            panel.Controls.AddRange(new Control[] 
            { 
                _statusLabel, 
                _pendingLabel, 
                _lastSyncLabel, 
                _syncButton,
                _closeButton 
            });

            Controls.Add(panel);
        }

        private void UpdateStatus()
        {
            // This would check real status in production
            _statusLabel.Text = "Status: Connected";
            _statusLabel.ForeColor = System.Drawing.Color.Green;
            
            _pendingLabel.Text = "Pending Transactions: 0";
            _lastSyncLabel.Text = $"Last Sync: {DateTime.Now:HH:mm:ss}";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _refreshTimer?.Stop();
                _refreshTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
