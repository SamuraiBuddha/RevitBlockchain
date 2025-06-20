# RevitBlockchain Deployment Guide

## Prerequisites

### Software Requirements
- **Autodesk Revit 2024** (or later)
- **Visual Studio 2022** with .NET desktop development workload
- **.NET Framework 4.8** SDK
- **PowerShell 5.0** or later
- **MCP Blockchain Server** (see [mcp-memory-blockchain](https://github.com/SamuraiBuddha/mcp-memory-blockchain))

### Development Environment
- Windows 10/11 (64-bit)
- Administrator access (for deployment)
- Network access to blockchain server

## Building the Project

### Using Visual Studio
1. Open `RevitBlockchain.csproj` in Visual Studio 2022
2. Set configuration to `Release`
3. Build → Build Solution (Ctrl+Shift+B)
4. Output will be in `bin\Release\`

### Using Command Line
```powershell
# From project root
msbuild RevitBlockchain.csproj /p:Configuration=Release
```

## Deployment Options

### Option 1: Automated Deployment (Recommended)

```powershell
# Run as Administrator
.\tools\deploy-to-revit.ps1 -RevitVersion 2024 -Configuration Release
```

This script will:
- Build the project (if needed)
- Close Revit if running
- Copy files to the correct location
- Create a desktop shortcut for future deployments
- Optionally start Revit

### Option 2: Manual Deployment

1. **Build the project** in Release mode

2. **Copy files** to Revit add-ins folder:
   ```
   C:\ProgramData\Autodesk\Revit\Addins\2024\
   ```
   
   Files to copy:
   - `RevitBlockchain.dll`
   - `RevitBlockchain.addin`
   - `Newtonsoft.Json.dll`
   - Any other dependencies

3. **Create Resources folder** for icons:
   ```
   C:\ProgramData\Autodesk\Revit\Addins\2024\Resources\
   ```

4. **Restart Revit**

### Option 3: Development Deployment

For development/debugging:

1. In Visual Studio, set project properties:
   - Debug → Start Action → Start external program
   - Browse to: `C:\Program Files\Autodesk\Revit 2024\Revit.exe`
   
2. Set post-build event:
   ```xml
   <PostBuildEvent>
   copy "$(TargetPath)" "$(ProgramData)\Autodesk\Revit\Addins\2024\"
   copy "$(ProjectDir)RevitBlockchain.addin" "$(ProgramData)\Autodesk\Revit\Addins\2024\"
   </PostBuildEvent>
   ```

3. Press F5 to build, deploy, and launch Revit with debugger attached

## Configuration

### Blockchain Server Connection

1. **Environment Variable** (Recommended):
   ```powershell
   [Environment]::SetEnvironmentVariable("BLOCKCHAIN_SERVER_URL", "http://your-server:3000", "User")
   ```

2. **Configuration File**:
   Create `%APPDATA%\RevitBlockchain\config.json`:
   ```json
   {
     "blockchainServer": "http://your-server:3000",
     "instanceId": "RevitClient-001",
     "autoSync": true,
     "syncInterval": 300,
     "offlineMode": false
   }
   ```

3. **Registry** (Enterprise deployment):
   ```
   HKEY_CURRENT_USER\Software\RevitBlockchain
   └── BlockchainServerUrl (REG_SZ): http://your-server:3000
   ```

## Importing Historical Data

To import existing central file history:

```powershell
.\tools\import-central-history.ps1 `
    -CentralFilePath "C:\Projects\MyProject_Central.rvt" `
    -BlockchainServer "http://localhost:3000" `
    -OutputPath ".\genesis_import.json"
```

## Verification

1. **Check Installation**:
   - Start Revit
   - Look for "Blockchain" tab in ribbon
   - Click "Status" button

2. **Test Connection**:
   - Status should show "Connected"
   - Check server logs for connection

3. **Test Sync Tracking**:
   - Open a workshared project
   - Make changes
   - Sync to central
   - Check blockchain for transaction

## Troubleshooting

### Add-in Not Loading

1. **Check add-in manifest**:
   - Verify `.addin` file is in correct location
   - Check XML syntax
   - Ensure assembly path is correct

2. **Check dependencies**:
   - All DLLs must be in add-ins folder
   - Use Dependency Walker to check missing DLLs

3. **Check Revit journal**:
   ```
   %LOCALAPPDATA%\Autodesk\Revit\Autodesk Revit 2024\Journals\
   ```

### Connection Issues

1. **Firewall**:
   - Allow outbound connections to blockchain server
   - Check Windows Defender Firewall rules

2. **Proxy**:
   - Configure system proxy settings
   - Set `HTTP_PROXY` environment variable

3. **SSL/TLS**:
   - For HTTPS, ensure certificates are trusted
   - Check TLS version compatibility

### Performance Issues

1. **Reduce sync frequency**:
   ```json
   {
     "syncInterval": 600,
     "batchSize": 50
   }
   ```

2. **Enable offline mode**:
   - Transactions queued locally
   - Synced when connection restored

3. **Optimize workset tracking**:
   - Disable real-time element tracking
   - Track only sync events

## Uninstallation

### Automated
```powershell
.\tools\uninstall.ps1 -RevitVersion 2024
```

### Manual
1. Close Revit
2. Delete files from:
   ```
   C:\ProgramData\Autodesk\Revit\Addins\2024\RevitBlockchain.*
   ```
3. Remove configuration:
   ```
   %APPDATA%\RevitBlockchain\
   ```

## Enterprise Deployment

For deploying to multiple workstations:

1. **Create MSI installer** using WiX Toolset
2. **Use Group Policy** to deploy MSI
3. **Configure via registry** using GPO
4. **Central blockchain server** accessible by all clients

See `tools\enterprise\` for deployment scripts and templates.

## Support

For issues or questions:
1. Check Revit journal files
2. Review blockchain server logs
3. Enable debug logging in config
4. Submit issue on GitHub
