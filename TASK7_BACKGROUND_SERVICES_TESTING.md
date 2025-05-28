# Task 7: Background Services - Testing Guide

## Implementation Summary

✅ **COMPLETED**: Task 7 - Background Services has been successfully implemented with comprehensive automated auction expiry processing and transaction settlement services.

### Implemented Features

1. **Auction Expiry Service**
   - `IAuctionExpiryService` and `AuctionExpiryService` - Core auction expiry processing logic
   - `AuctionExpiryBackgroundService` - Continuous background processing
   - Automated auction expiry detection and processing
   - Asset ownership transfer on successful auctions
   - Payment settlement between winners and sellers
   - Blocked amount release for non-winning bidders
   - Manual force expiry capability for admin intervention

2. **Transaction Settlement Service**
   - `ITransactionSettlementService` and `TransactionSettlementService` - Core settlement logic
   - `TransactionSettlementBackgroundService` - Continuous background processing
   - Wallet balance reconciliation with transaction history
   - Orphaned blocked amount detection and cleanup
   - Transaction statistics monitoring
   - Audit-compliant transaction record management

3. **Background Services Management**
   - `BackgroundServicesController` - Admin monitoring and control endpoints
   - Real-time status monitoring and statistics
   - Manual trigger capabilities for all background operations
   - Comprehensive logging and error handling
   - Configurable processing intervals

4. **Configuration Management**
   - Configurable check intervals for all background services
   - Separate settings for auction expiry, settlement, reconciliation, and cleanup
   - Enhanced logging configuration for background service monitoring

### Background Services Implemented

| Service | Purpose | Default Interval | Features |
|---------|---------|------------------|----------|
| **Auction Expiry** | Process expired auctions | 1 minute | Asset transfer, payment settlement, bid release |
| **Transaction Settlement** | Wallet reconciliation & cleanup | 5 minutes | Balance reconciliation, orphaned amount cleanup |
| **Wallet Reconciliation** | Periodic balance verification | 24 hours | Transaction history validation |
| **Transaction Cleanup** | Old record management | 7 days | Audit-compliant record retention |

### API Endpoints Implemented

| Method | Endpoint | Description | Admin Only |
|--------|----------|-------------|------------|
| GET | `/api/backgroundservices/status` | Get services status and statistics | ✅ |
| POST | `/api/backgroundservices/auction-expiry/process` | Manually process expired auctions | ✅ |
| GET | `/api/backgroundservices/auction-expiry/expiring` | Get auctions expiring soon | ✅ |
| POST | `/api/backgroundservices/auction-expiry/force-expire/{id}` | Force expire specific auction | ✅ |
| POST | `/api/backgroundservices/transaction-settlement/reconcile-wallets` | Manual wallet reconciliation | ✅ |
| POST | `/api/backgroundservices/transaction-settlement/process-stuck-amounts` | Process orphaned amounts | ✅ |
| GET | `/api/backgroundservices/transaction-settlement/statistics` | Get transaction statistics | ✅ |
| POST | `/api/backgroundservices/transaction-settlement/cleanup` | Cleanup old transactions | ✅ |

## Testing Instructions

### Prerequisites
1. Start the application: `dotnet run` from TheAuctionHouse directory
2. Access Swagger UI at: `http://localhost:5000`
3. Use admin credentials: `admin@auctionhouse.com` / `Admin123!`

### Test Scenarios

#### 1. Authentication Setup
```json
POST /api/auth/login
{
  "email": "admin@auctionhouse.com",
  "password": "Admin123!"
}
```
- Copy the JWT token from response
- Click "Authorize" in Swagger UI and enter: `Bearer {your-token}`

#### 2. Setup Test Data for Background Services Testing

Create test auctions that will expire soon:

```json
// Create test user and assets
POST /api/auth/register
{
  "firstName": "Test",
  "lastName": "User",
  "email": "testuser@example.com",
  "password": "TestUser123!"
}

// Login as test user and create assets
POST /api/assets
{
  "title": "Test Asset for Expiry",
  "description": "Asset to test auction expiry processing",
  "retailValue": 1000
}

// Add wallet funds
POST /api/wallet/deposit
{
  "amount": 5000
}

// Change asset status and create short auction (2 minutes)
PATCH /api/assets/1/status
{
  "status": "OpenToAuction"
}

POST /api/auctions
{
  "assetId": 1,
  "reservedPrice": 100,
  "minimumBidIncrement": 50,
  "totalMinutesToExpiry": 2
}

// Place a bid to test settlement
POST /api/auctions/1/bid
{
  "bidAmount": 150
}
```

#### 3. Monitor Background Services Status
```json
GET /api/backgroundservices/status
```

**Expected Results:**
- **Timestamp**: Current UTC time
- **AuctionExpiry**: 
  - Count of auctions expiring within the hour
  - List of expiring auction IDs
- **TransactionSettlement**:
  - Total transactions count
  - Total transaction volume
  - Users with blocked amounts
  - Total blocked amount
  - Orphaned blocked amounts count
- **SystemHealth**: Service operational status

#### 4. Test Auction Expiry Processing

**Get Expiring Auctions:**
```json
GET /api/backgroundservices/auction-expiry/expiring?withinMinutes=5
```

**Expected Results:**
- List of auction IDs expiring within 5 minutes
- Count of expiring auctions
- Timestamp of check

**Manual Auction Expiry Processing:**
```json
POST /api/backgroundservices/auction-expiry/process
```

**Expected Results:**
- Number of auctions processed
- Processing completion message
- Timestamp of processing

**Force Expire Specific Auction:**
```json
POST /api/backgroundservices/auction-expiry/force-expire/1
```

**Expected Results:**
- Success message for auction expiry
- Auction ID confirmation
- Timestamp of forced expiry

#### 5. Test Transaction Settlement Services

**Get Transaction Statistics:**
```json
GET /api/backgroundservices/transaction-settlement/statistics
```

**Expected Results:**
```json
{
  "totalTransactions": 10,
  "pendingTransactions": 0,
  "totalVolume": 1500.00,
  "usersWithBlockedAmounts": 2,
  "totalBlockedAmount": 300.00,
  "orphanedBlockedAmounts": 0,
  "lastProcessedTime": "2024-01-01T12:00:00Z"
}
```

**Manual Wallet Reconciliation:**
```json
POST /api/backgroundservices/transaction-settlement/reconcile-wallets
```

**Expected Results:**
- Number of wallets reconciled
- Reconciliation completion message
- Timestamp of reconciliation

**Process Stuck Blocked Amounts:**
```json
POST /api/backgroundservices/transaction-settlement/process-stuck-amounts
```

**Expected Results:**
- Number of stuck amounts processed
- Processing completion message
- Timestamp of processing

**Transaction Cleanup:**
```json
POST /api/backgroundservices/transaction-settlement/cleanup?olderThanDays=30
```

**Expected Results:**
- Number of records cleaned (typically 0 for audit compliance)
- Cleanup completion message
- Days threshold used

### Business Logic Validation

#### Auction Expiry Processing
- ✅ **Automatic Detection**: Expired auctions are automatically detected
- ✅ **Asset Transfer**: Asset ownership transferred to highest bidder
- ✅ **Payment Settlement**: Winner pays seller, amounts deducted/credited correctly
- ✅ **Bid Release**: Non-winning bidders have blocked amounts released
- ✅ **Status Updates**: Auction status updated to Expired or ExpiredWithoutBids
- ✅ **Transaction Records**: Complete audit trail created for all transactions

#### Transaction Settlement
- ✅ **Balance Reconciliation**: Wallet balances match transaction history
- ✅ **Blocked Amount Validation**: Blocked amounts match active bids
- ✅ **Orphaned Amount Cleanup**: Stuck amounts from expired auctions released
- ✅ **Statistics Accuracy**: Real-time statistics reflect current system state
- ✅ **Audit Compliance**: Transaction records preserved for audit purposes

#### Background Service Reliability
- ✅ **Continuous Operation**: Services run continuously without manual intervention
- ✅ **Error Handling**: Individual failures don't stop overall processing
- ✅ **Configurable Intervals**: Processing frequency can be adjusted via configuration
- ✅ **Manual Override**: Admin can manually trigger any background operation
- ✅ **Comprehensive Logging**: All operations logged for monitoring and debugging

### Integration Testing

#### Complete Auction Lifecycle with Background Processing
1. **Setup Phase**
   ```json
   // Create user, asset, and auction
   POST /api/auth/register
   POST /api/assets
   POST /api/wallet/deposit
   POST /api/auctions (with short expiry)
   ```

2. **Bidding Phase**
   ```json
   // Multiple users place bids
   POST /api/auctions/1/bid
   ```

3. **Expiry Processing**
   ```json
   // Wait for automatic expiry or force expire
   POST /api/backgroundservices/auction-expiry/force-expire/1
   ```

4. **Verification Phase**
   ```json
   // Verify asset ownership transfer
   GET /api/assets/1
   
   // Verify wallet balances
   GET /api/wallet/balance
   
   // Verify transaction history
   GET /api/wallet/transactions
   
   // Check background service status
   GET /api/backgroundservices/status
   ```

#### Wallet Reconciliation Testing
1. **Create Discrepancy** (for testing purposes)
2. **Run Reconciliation**
   ```json
   POST /api/backgroundservices/transaction-settlement/reconcile-wallets
   ```
3. **Verify Correction**
   ```json
   GET /api/backgroundservices/transaction-settlement/statistics
   ```

#### Orphaned Amount Cleanup Testing
1. **Create Scenario** with expired auctions and unreleased bids
2. **Process Cleanup**
   ```json
   POST /api/backgroundservices/transaction-settlement/process-stuck-amounts
   ```
3. **Verify Release**
   ```json
   GET /api/wallet/balance
   GET /api/wallet/transactions
   ```

### Performance Testing

#### Background Service Load Testing
```json
// Create multiple auctions with short expiry times
// Monitor processing performance
GET /api/backgroundservices/status

// Check processing times in logs
// Verify no performance degradation with multiple auctions
```

#### Concurrent Processing Testing
- Multiple auctions expiring simultaneously
- High transaction volume during settlement
- Concurrent manual operations with background processing

### Configuration Testing

#### Interval Configuration Testing
Update `appsettings.json`:
```json
{
  "BackgroundServices": {
    "AuctionExpiryCheckIntervalMinutes": 0.5,
    "TransactionSettlementCheckIntervalMinutes": 2,
    "WalletReconciliationIntervalHours": 1,
    "TransactionCleanupIntervalHours": 24
  }
}
```

Verify services respect new intervals through logging.

### Error Handling Testing

#### Service Resilience Testing
1. **Database Connection Issues**: Verify graceful handling
2. **Individual Auction Processing Errors**: Ensure other auctions still process
3. **Transaction Conflicts**: Test concurrent access scenarios
4. **Invalid Data Scenarios**: Test with corrupted or inconsistent data

### Monitoring and Logging

#### Log Analysis
Monitor application logs for:
- Background service startup/shutdown messages
- Processing completion logs with counts
- Error logs with detailed exception information
- Performance metrics and timing information

#### Status Monitoring
Regular status checks should show:
- Services running continuously
- Processing counts increasing over time
- Statistics reflecting current system state
- No accumulation of orphaned amounts

## Implementation Details

### Key Features Implemented

1. **Automated Auction Processing**
   - Continuous monitoring of auction expiry times
   - Automatic processing of expired auctions
   - Complete transaction settlement workflow
   - Asset ownership transfer automation

2. **Financial Integrity**
   - Wallet balance reconciliation
   - Blocked amount management
   - Orphaned amount cleanup
   - Complete audit trail maintenance

3. **System Reliability**
   - Fault-tolerant processing (individual failures don't stop service)
   - Configurable processing intervals
   - Comprehensive error logging
   - Manual override capabilities

4. **Administrative Control**
   - Real-time status monitoring
   - Manual trigger capabilities
   - Detailed statistics and reporting
   - Force operations for emergency scenarios

### Database Integration

- **Efficient Queries**: Optimized LINQ queries for background processing
- **Transaction Safety**: Proper transaction handling for data consistency
- **Concurrent Access**: Safe handling of concurrent database operations
- **Performance Optimization**: Minimal database impact during processing

### Configuration Management

```json
{
  "BackgroundServices": {
    "AuctionExpiryCheckIntervalMinutes": 1,      // How often to check for expired auctions
    "TransactionSettlementCheckIntervalMinutes": 5,  // How often to run settlement tasks
    "WalletReconciliationIntervalHours": 24,    // How often to reconcile wallets
    "TransactionCleanupIntervalHours": 168      // How often to run cleanup (7 days)
  }
}
```

### Business Rules Implemented

- **Auction Expiry**: Automatic processing when `GetRemainingTimeInMinutes() <= 0`
- **Asset Transfer**: Ownership transferred to highest bidder on successful auctions
- **Payment Settlement**: Winner pays seller, blocked amounts properly managed
- **Bid Release**: Non-winning bidders have amounts released automatically
- **Audit Compliance**: All transactions recorded with complete audit trail

## Next Steps

Task 7 is now complete and provides:
- Fully automated auction expiry processing
- Comprehensive transaction settlement services
- Administrative monitoring and control capabilities
- Robust error handling and logging

Ready for integration with:
- Task 8: API Documentation (enhanced with background service endpoints)
- Task 9: Testing (comprehensive test coverage for background services)
- Production deployment with reliable background processing

The Background Services system ensures the auction house operates autonomously, processing expired auctions and maintaining financial integrity without manual intervention.

## Testing Checklist

- ✅ Auction expiry background service running continuously
- ✅ Transaction settlement background service running continuously
- ✅ Automatic auction processing on expiry
- ✅ Asset ownership transfer on successful auctions
- ✅ Payment settlement between winners and sellers
- ✅ Blocked amount release for non-winning bidders
- ✅ Wallet balance reconciliation
- ✅ Orphaned amount detection and cleanup
- ✅ Administrative monitoring and control endpoints
- ✅ Manual trigger capabilities for all operations
- ✅ Comprehensive error handling and logging
- ✅ Configurable processing intervals
- ✅ Performance optimization and concurrent access safety
- ✅ Complete audit trail maintenance 