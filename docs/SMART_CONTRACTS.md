# Smart Contract Architecture

## Overview

RevitBlockchain implements smart contracts to automate and enforce rules around BIM collaboration. These contracts run on the MCP blockchain infrastructure and are called by the Revit add-in during key operations.

## Contract Design Principles

1. **Simplicity**: Contracts should be easy to understand and audit
2. **Gas Efficiency**: Minimize computational overhead
3. **Deterministic**: Same inputs always produce same outputs
4. **Upgradeable**: Can evolve without breaking existing data

## Core Contracts

### 1. WorksetOwnershipContract

**Purpose**: Manages ownership and borrowing of worksets

**State Variables**:
```python
workset_owners: Dict[workset_id, user_id]
workset_borrowers: Dict[workset_id, List[user_id]]
borrow_requests: List[BorrowRequest]
borrow_history: List[BorrowTransaction]
```

**Key Functions**:
- `request_ownership(workset_id, user_id)` - Request to own a workset
- `transfer_ownership(workset_id, from_user, to_user)` - Transfer ownership
- `borrow_elements(workset_id, element_ids, user_id)` - Borrow specific elements
- `release_borrowed(workset_id, element_ids, user_id)` - Release borrowed elements
- `force_release(workset_id, admin_signature)` - Admin override for stuck borrowing

**Rules**:
- Only workset owner can grant borrowing permissions
- Borrowed elements automatically released on sync
- Ownership transfers require both parties to sign
- 24-hour timeout on unsynced borrowed elements

### 2. ElementModificationContract

**Purpose**: Record and validate all element changes

**State Variables**:
```python
element_hashes: Dict[element_id, hash]
modification_log: List[ModificationRecord]
element_lineage: Dict[element_id, List[version_hash]]
```

**Key Functions**:
- `record_modification(element_id, old_hash, new_hash, user_id, timestamp)`
- `validate_change(element_id, proposed_hash)` - Check if change is valid
- `get_element_history(element_id)` - Full modification history
- `rollback_element(element_id, target_version)` - Revert to previous state

**Validation Rules**:
- Hash must match expected value (prevents tampering)
- User must have borrowing rights
- Timestamp must be within sync window
- Changes must not conflict with other pending modifications

### 3. SyncApprovalContract

**Purpose**: Multi-signature approvals for critical changes

**State Variables**:
```python
approval_rules: Dict[element_category, ApprovalRule]
pending_approvals: List[ApprovalRequest]
approval_votes: Dict[request_id, List[Vote]]
```

**Key Functions**:
- `create_approval_request(changes, user_id, justification)`
- `vote_on_request(request_id, approve: bool, user_id, comment)`
- `execute_approved_changes(request_id)` - Apply approved changes
- `set_approval_rules(category, min_approvals, authorized_users)`

**Approval Categories**:
- Structural elements: Requires 2 structural engineers
- MEP systems: Requires MEP coordinator approval
- Facade changes: Requires architect + client approval
- Core & Shell: Requires project manager + lead architect

### 4. ConflictResolutionContract

**Purpose**: Automated handling of concurrent edit conflicts

**State Variables**:
```python
conflict_queue: List[ConflictRecord]
resolution_strategies: Dict[conflict_type, ResolutionStrategy]
resolution_history: List[ResolutionRecord]
```

**Key Functions**:
- `detect_conflict(element_id, changes_a, changes_b)` - Identify conflicts
- `auto_resolve(conflict_id)` - Apply automated resolution
- `escalate_conflict(conflict_id, reason)` - Escalate to human review
- `manual_resolve(conflict_id, resolution, user_id)`

**Resolution Strategies**:
1. **Last Write Wins**: For non-critical elements
2. **Merge Changes**: For compatible modifications
3. **Prioritize by Role**: Structural > MEP > Architectural
4. **Escalate**: For critical conflicts requiring human judgment

### 5. ComplianceCheckContract

**Purpose**: Ensure all changes meet project standards

**State Variables**:
```python
project_standards: Dict[standard_id, ComplianceRule]
violation_log: List[ViolationRecord]
exemptions: Dict[element_id, ExemptionRecord]
```

**Key Functions**:
- `check_compliance(element_id, changes)` - Validate against standards
- `report_violation(element_id, standard_id, details)`
- `request_exemption(element_id, standard_id, justification)`
- `update_standards(standard_id, new_rules, admin_signature)`

**Compliance Checks**:
- Naming conventions
- Parameter requirements
- Geometric constraints
- Material specifications
- System performance criteria

## Transaction Format

All contract calls generate blockchain transactions:

```json
{
  "transaction_id": "1750399939668811-RevitClient-001-a3f8b2c1",
  "contract_address": "0xWorksetOwnership",
  "function": "transfer_ownership",
  "params": {
    "workset_id": "WS-001",
    "from_user": "john.doe@example.com",
    "to_user": "jane.smith@example.com"
  },
  "signatures": [
    {"user": "john.doe@example.com", "sig": "0x..."},
    {"user": "jane.smith@example.com", "sig": "0x..."}
  ],
  "timestamp": 1750399939668811,
  "block_height": 12345
}
```

## Gas Costs

Estimated transaction costs:
- Simple state change: 100 gas
- Multi-sig approval: 500 gas
- Conflict resolution: 300 gas
- Compliance check: 200 gas per rule

## Integration with Revit

The Revit add-in calls contracts through the MCP blockchain connector:

```csharp
// Example: Request workset ownership
var result = await blockchainClient.CallContract(
    "WorksetOwnershipContract",
    "request_ownership",
    new { workset_id = "WS-001", user_id = currentUser.Id }
);
```

## Security Considerations

1. **Authentication**: All users must have valid Revit licenses and blockchain keys
2. **Authorization**: Role-based access control enforced by contracts
3. **Audit Trail**: Every contract interaction is logged
4. **Immutability**: Contract state changes cannot be reversed without consensus

## Future Enhancements

- **AI-Powered Conflict Resolution**: Use ML to predict and prevent conflicts
- **Cross-Project Standards**: Share compliance rules across projects
- **Performance Optimization**: Batch transactions for efficiency
- **Integration with External Systems**: Connect to ERP, scheduling software
