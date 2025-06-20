using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using RevitBlockchain.Core;

namespace RevitBlockchain.RevitIntegration
{
    /// <summary>
    /// Tracks workset ownership and changes
    /// </summary>
    public class WorksetTracker
    {
        private readonly Document _document;
        private readonly BlockchainClient _blockchainClient;
        private Dictionary<int, WorksetInfo> _worksetState;

        public WorksetTracker(Document document, BlockchainClient blockchainClient)
        {
            _document = document;
            _blockchainClient = blockchainClient;
            _worksetState = new Dictionary<int, WorksetInfo>();
        }

        /// <summary>
        /// Initialize workset tracking
        /// </summary>
        public void Initialize()
        {
            if (!_document.IsWorkshared) return;

            // Capture initial workset state
            RefreshWorksetState();

            // Register initial state to blockchain
            Task.Run(async () => await RegisterWorksetState());
        }

        /// <summary>
        /// Refresh current workset state
        /// </summary>
        public void RefreshWorksetState()
        {
            _worksetState.Clear();

            var worksets = new FilteredWorksetCollector(_document)
                .OfKind(WorksetKind.UserWorkset)
                .Cast<Workset>();

            foreach (var workset in worksets)
            {
                var info = new WorksetInfo
                {
                    Id = workset.Id.IntegerValue,
                    Name = workset.Name,
                    Owner = workset.Owner,
                    IsEditable = workset.IsEditable,
                    IsOpen = workset.IsOpen,
                    UniqueId = workset.UniqueId.ToString()
                };

                // Get borrowed elements
                info.BorrowedElements = GetBorrowedElements(workset);
                
                _worksetState[info.Id] = info;
            }
        }

        /// <summary>
        /// Check for workset ownership changes
        /// </summary>
        public async Task<List<WorksetChange>> CheckForChanges()
        {
            var changes = new List<WorksetChange>();
            var previousState = new Dictionary<int, WorksetInfo>(_worksetState);
            
            // Refresh to get current state
            RefreshWorksetState();

            // Compare states
            foreach (var current in _worksetState.Values)
            {
                if (previousState.TryGetValue(current.Id, out var previous))
                {
                    if (current.Owner != previous.Owner)
                    {
                        changes.Add(new WorksetChange
                        {
                            WorksetId = current.Id,
                            WorksetName = current.Name,
                            ChangeType = WorksetChangeType.OwnershipTransfer,
                            PreviousOwner = previous.Owner,
                            NewOwner = current.Owner,
                            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000
                        });

                        // Call smart contract for ownership transfer
                        await RecordOwnershipTransfer(current, previous.Owner);
                    }

                    if (current.IsEditable != previous.IsEditable)
                    {
                        changes.Add(new WorksetChange
                        {
                            WorksetId = current.Id,
                            WorksetName = current.Name,
                            ChangeType = WorksetChangeType.EditabilityChange,
                            IsNowEditable = current.IsEditable,
                            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000
                        });
                    }

                    // Check for borrowed element changes
                    var borrowedChanges = CheckBorrowedElementChanges(current, previous);
                    changes.AddRange(borrowedChanges);
                }
                else
                {
                    // New workset
                    changes.Add(new WorksetChange
                    {
                        WorksetId = current.Id,
                        WorksetName = current.Name,
                        ChangeType = WorksetChangeType.Created,
                        NewOwner = current.Owner,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000
                    });
                }
            }

            // Check for deleted worksets
            foreach (var previous in previousState.Values)
            {
                if (!_worksetState.ContainsKey(previous.Id))
                {
                    changes.Add(new WorksetChange
                    {
                        WorksetId = previous.Id,
                        WorksetName = previous.Name,
                        ChangeType = WorksetChangeType.Deleted,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000
                    });
                }
            }

            return changes;
        }

        /// <summary>
        /// Get elements borrowed from a workset
        /// </summary>
        private List<BorrowedElementInfo> GetBorrowedElements(Workset workset)
        {
            var borrowedElements = new List<BorrowedElementInfo>();

            // Get all elements in this workset
            var collector = new FilteredElementCollector(_document)
                .WhereElementIsNotElementType();

            foreach (var element in collector)
            {
                if (element.WorksetId.IntegerValue == workset.Id.IntegerValue)
                {
                    var checkoutStatus = WorksharingUtils.GetCheckoutStatus(_document, element.Id);
                    if (checkoutStatus == CheckoutStatus.OwnedByOtherUser)
                    {
                        var owner = WorksharingUtils.GetWorksharingTooltipInfo(_document, element.Id).Owner;
                        borrowedElements.Add(new BorrowedElementInfo
                        {
                            ElementId = element.Id.IntegerValue,
                            ElementUniqueId = element.UniqueId,
                            BorrowedBy = owner,
                            ElementName = element.Name
                        });
                    }
                }
            }

            return borrowedElements;
        }

        /// <summary>
        /// Check for changes in borrowed elements
        /// </summary>
        private List<WorksetChange> CheckBorrowedElementChanges(WorksetInfo current, WorksetInfo previous)
        {
            var changes = new List<WorksetChange>();

            // Find newly borrowed elements
            var newlyBorrowed = current.BorrowedElements
                .Where(c => !previous.BorrowedElements.Any(p => p.ElementUniqueId == c.ElementUniqueId))
                .ToList();

            foreach (var borrowed in newlyBorrowed)
            {
                changes.Add(new WorksetChange
                {
                    WorksetId = current.Id,
                    WorksetName = current.Name,
                    ChangeType = WorksetChangeType.ElementBorrowed,
                    ElementId = borrowed.ElementId,
                    BorrowedBy = borrowed.BorrowedBy,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000
                });
            }

            // Find released elements
            var released = previous.BorrowedElements
                .Where(p => !current.BorrowedElements.Any(c => c.ElementUniqueId == p.ElementUniqueId))
                .ToList();

            foreach (var rel in released)
            {
                changes.Add(new WorksetChange
                {
                    WorksetId = current.Id,
                    WorksetName = current.Name,
                    ChangeType = WorksetChangeType.ElementReleased,
                    ElementId = rel.ElementId,
                    ReleasedBy = rel.BorrowedBy,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000
                });
            }

            return changes;
        }

        /// <summary>
        /// Register current workset state to blockchain
        /// </summary>
        private async Task RegisterWorksetState()
        {
            foreach (var workset in _worksetState.Values)
            {
                await _blockchainClient.CallContract(
                    "WorksetOwnershipContract",
                    "register_workset",
                    new
                    {
                        workset_id = workset.Id,
                        workset_name = workset.Name,
                        owner = workset.Owner,
                        is_editable = workset.IsEditable,
                        document_guid = _document.GetCloudModelPath()?.GetModelGUID()?.ToString() ?? "local"
                    }
                );
            }
        }

        /// <summary>
        /// Record ownership transfer to blockchain
        /// </summary>
        private async Task RecordOwnershipTransfer(WorksetInfo workset, string previousOwner)
        {
            await _blockchainClient.CallContract(
                "WorksetOwnershipContract",
                "transfer_ownership",
                new
                {
                    workset_id = workset.Id,
                    from_user = previousOwner,
                    to_user = workset.Owner,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000,
                    document_guid = _document.GetCloudModelPath()?.GetModelGUID()?.ToString() ?? "local"
                }
            );
        }
    }

    /// <summary>
    /// Workset information
    /// </summary>
    public class WorksetInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Owner { get; set; }
        public bool IsEditable { get; set; }
        public bool IsOpen { get; set; }
        public string UniqueId { get; set; }
        public List<BorrowedElementInfo> BorrowedElements { get; set; } = new List<BorrowedElementInfo>();
    }

    /// <summary>
    /// Borrowed element information
    /// </summary>
    public class BorrowedElementInfo
    {
        public int ElementId { get; set; }
        public string ElementUniqueId { get; set; }
        public string BorrowedBy { get; set; }
        public string ElementName { get; set; }
    }

    /// <summary>
    /// Workset change information
    /// </summary>
    public class WorksetChange
    {
        public int WorksetId { get; set; }
        public string WorksetName { get; set; }
        public WorksetChangeType ChangeType { get; set; }
        public string PreviousOwner { get; set; }
        public string NewOwner { get; set; }
        public bool IsNowEditable { get; set; }
        public int ElementId { get; set; }
        public string BorrowedBy { get; set; }
        public string ReleasedBy { get; set; }
        public long Timestamp { get; set; }
    }

    public enum WorksetChangeType
    {
        Created,
        Deleted,
        OwnershipTransfer,
        EditabilityChange,
        ElementBorrowed,
        ElementReleased
    }
}
