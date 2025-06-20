using System;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using RevitBlockchain.Core;

namespace RevitBlockchain.Commands
{
    /// <summary>
    /// Command to view element modification history from blockchain
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ElementHistoryCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData.Application;
                var uiDoc = uiApp.ActiveUIDocument;
                var doc = uiDoc.Document;

                // Get blockchain client
                var blockchainClient = Application.GetBlockchainClient();
                if (blockchainClient == null)
                {
                    TaskDialog.Show("Error", "Blockchain client not initialized.");
                    return Result.Failed;
                }

                // Prompt user to select an element
                Reference reference = null;
                try
                {
                    reference = uiDoc.Selection.PickObject(
                        ObjectType.Element,
                        "Select an element to view its blockchain history"
                    );
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }

                if (reference == null)
                {
                    return Result.Cancelled;
                }

                // Get the selected element
                var element = doc.GetElement(reference);
                if (element == null)
                {
                    TaskDialog.Show("Error", "Failed to get selected element.");
                    return Result.Failed;
                }

                // Get element history from blockchain
                var history = blockchainClient.GetElementHistory(element.UniqueId).Result;

                // Display history
                DisplayElementHistory(element, history);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        private void DisplayElementHistory(Element element, System.Collections.Generic.List<ElementHistory> history)
        {
            // Create a simple form to display history
            var form = new Form
            {
                Text = $"Blockchain History: {element.Name ?? element.Id.ToString()}",
                Size = new System.Drawing.Size(800, 600),
                StartPosition = FormStartPosition.CenterScreen
            };

            // Create list view
            var listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };

            // Add columns
            listView.Columns.Add("Timestamp", 150);
            listView.Columns.Add("Modified By", 120);
            listView.Columns.Add("Change Type", 100);
            listView.Columns.Add("Transaction ID", 200);
            listView.Columns.Add("Details", 200);

            // Add history items
            if (history != null && history.Any())
            {
                foreach (var item in history.OrderByDescending(h => h.Timestamp))
                {
                    var listItem = new ListViewItem(new[]
                    {
                        item.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        item.ModifiedBy,
                        item.ChangeType,
                        item.TransactionId,
                        GetChangeDetails(item.Changes)
                    });
                    listView.Items.Add(listItem);
                }
            }
            else
            {
                // Show element info if no blockchain history
                var info = new StringBuilder();
                info.AppendLine($"Element: {element.Name ?? "Unnamed"}");
                info.AppendLine($"ID: {element.Id}");
                info.AppendLine($"Unique ID: {element.UniqueId}");
                info.AppendLine($"Category: {element.Category?.Name ?? "N/A"}");
                info.AppendLine();
                info.AppendLine("No blockchain history found for this element.");
                info.AppendLine();
                info.AppendLine("Blockchain tracking begins after the add-in is installed.");
                info.AppendLine("Historical data can be imported using the import tool.");

                var label = new Label
                {
                    Text = info.ToString(),
                    Dock = DockStyle.Fill,
                    Padding = new Padding(20),
                    Font = new System.Drawing.Font("Consolas", 10)
                };
                form.Controls.Add(label);
            }

            if (history?.Any() == true)
            {
                form.Controls.Add(listView);
            }

            // Add close button
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50
            };

            var closeButton = new Button
            {
                Text = "Close",
                DialogResult = DialogResult.OK,
                Size = new System.Drawing.Size(100, 30)
            };
            closeButton.Location = new System.Drawing.Point(
                (buttonPanel.Width - closeButton.Width) / 2,
                10
            );

            buttonPanel.Controls.Add(closeButton);
            form.Controls.Add(buttonPanel);

            form.ShowDialog();
        }

        private string GetChangeDetails(System.Collections.Generic.Dictionary<string, object> changes)
        {
            if (changes == null || !changes.Any())
                return "No details";

            var details = new StringBuilder();
            foreach (var kvp in changes.Take(3)) // Show first 3 changes
            {
                details.Append($"{kvp.Key}: {kvp.Value}; ");
            }

            if (changes.Count > 3)
                details.Append("...");

            return details.ToString().TrimEnd(';', ' ');
        }
    }
}
