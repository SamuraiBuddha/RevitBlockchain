# RevitBlockchain Usage Guide

## Getting Started

Once RevitBlockchain is installed, you'll see a new "Blockchain" tab in the Revit ribbon.

## Ribbon Interface

### Blockchain Tab

#### Status Button
- **Purpose**: Check connection to blockchain server
- **Shows**:
  - Connection status (Connected/Disconnected)
  - Pending transactions count
  - Last sync timestamp
  - Server URL
- **Auto-refresh**: Updates every 5 seconds

#### Force Sync Button
- **Purpose**: Manually trigger blockchain synchronization
- **Use when**:
  - Offline transactions are queued
  - Connection was restored
  - Before closing Revit
- **Action**: Submits all pending transactions

#### Element History Button
- **Purpose**: View complete modification history of an element
- **Usage**:
  1. Click the button
  2. Select any element in the model
  3. View history in the dialog
- **Shows**:
  - Timestamp of each change
  - User who made the change
  - Type of modification
  - Transaction ID on blockchain
  - Changed parameters

#### Audit Report Button
- **Purpose**: Generate comprehensive audit reports
- **Options**:
  - Date range selection
  - Filter by user
  - Filter by element category
  - Export formats (PDF, Excel, CSV)
- **Includes**:
  - Summary statistics
  - Detailed change log
  - Workset ownership history
  - Conflict resolutions

#### Settings Button
- **Purpose**: Configure blockchain integration
- **Settings**:
  - Server URL
  - Authentication credentials
  - Sync frequency
  - Offline mode options
  - Element tracking filters

## Working with Blockchain Tracking

### Automatic Tracking

RevitBlockchain automatically tracks:

1. **Synchronization Events**
   - Every sync to central
   - Duration and user
   - Elements modified/added/deleted
   - Workset changes

2. **Workset Changes**
   - Ownership transfers
   - Borrowing/releasing elements
   - Editability changes

3. **Element Modifications**
   - Parameter changes
   - Geometry modifications
   - Deletion/recreation

### Understanding Blockchain Records

#### Transaction Structure
```json
{
  "transaction_id": "1750400000000000-RevitClient-001-a1b2c3d4",
  "type": "SyncCompleted",
  "timestamp": "2024-06-20T10:30:00.000Z",
  "user": "john.doe@company.com",
  "data": {
    "elementsModified": 42,
    "worksetChanges": [...],
    "duration": 45.2
  }
}
```

#### Element History Entry
```json
{
  "elementId": "a3f8e9c1-1234-5678-9abc-def012345678",
  "modifiedBy": "jane.smith@company.com",
  "timestamp": "2024-06-20T10:30:00.000Z",
  "changeType": "Parameter",
  "changes": {
    "Height": {
      "old": "3000",
      "new": "3500"
    }
  }
}
```

## Smart Contract Features

### Workset Ownership

1. **Claiming Ownership**
   - Happens automatically when creating workset
   - Can transfer to another user
   - Requires both parties to be online

2. **Borrowing Elements**
   - Request specific elements
   - Owner receives notification
   - Auto-expire after 24 hours
   - Released on sync

### Approval Workflows

For critical elements (configured by admin):

1. **Multi-signature Approvals**
   - Structural changes need 2 engineers
   - MEP changes need coordinator approval
   - Client approval for facade changes

2. **Approval Process**
   - Make changes locally
   - Submit for approval on sync
   - Approvers notified
   - Changes held until approved

### Conflict Resolution

1. **Automatic Resolution**
   - Non-conflicting changes merged
   - Parameter precedence rules
   - Geometry union operations

2. **Manual Resolution Required**
   - Overlapping geometry changes
   - Conflicting parameter values
   - Deletion vs modification

## Best Practices

### 1. Regular Synchronization
- Sync at least every 2 hours
- Always sync before leaving
- Sync after major changes

### 2. Meaningful Sync Comments
```
Good: "Updated Level 2 floor plan - relocated columns per RFI-042"
Bad: "Changes"
```

### 3. Workset Management
- Keep worksets focused (one discipline/area)
- Release borrowed elements promptly
- Transfer ownership before vacation

### 4. Conflict Avoidance
- Communicate before major changes
- Check element history before modifying
- Use workset ownership properly

## Viewing Blockchain Data

### In Revit

1. **Element Properties**
   - Right-click → "Blockchain History"
   - Shows last 10 changes
   - Link to full history

2. **Project Browser Extension**
   - Blockchain node shows statistics
   - Recent changes by category
   - Active users

### Web Dashboard

Access `http://your-blockchain-server:3000/dashboard`

- Real-time activity feed
- Project statistics
- User activity graphs
- Search and filter tools

### API Access

For custom reporting:

```http
GET /api/element_history/{element_id}
GET /api/project_stats/{project_guid}
GET /api/user_activity/{user_id}
```

## Troubleshooting Common Issues

### "Offline Mode" Message
- Check network connection
- Verify server URL in settings
- Check firewall settings
- Transactions will queue and sync later

### "Transaction Failed" Error
- Check blockchain server logs
- Verify user permissions
- Ensure element isn't locked
- Try Force Sync

### Missing History
- History starts from add-in installation
- Import historical data using import tool
- Check element was tracked (not view-specific)

### Performance Impact
- Disable real-time tracking in settings
- Increase sync interval
- Filter tracked categories
- Use offline mode for slow connections

## Advanced Features

### Custom Smart Contracts

Add project-specific rules:

1. Create Python contract file
2. Deploy to blockchain server
3. Configure in Revit settings
4. Rules enforced automatically

### Integration with Other Systems

- **ERP Integration**: Cost tracking per change
- **Schedule Integration**: Link changes to timeline
- **Issue Tracking**: Connect RFIs to changes
- **Document Management**: Link drawings to model state

### Compliance Reporting

Generate reports for:
- ISO 19650 compliance
- Audit trails for disputes
- Change order documentation
- Regulatory submissions

## Tips for Teams

1. **Onboarding New Users**
   - Show Element History feature first
   - Explain workset ownership
   - Demonstrate audit reports

2. **Establishing Protocols**
   - Define sync frequency standards
   - Set approval thresholds
   - Create naming conventions

3. **Monitoring Usage**
   - Weekly audit report reviews
   - Track sync frequency by user
   - Identify training needs
