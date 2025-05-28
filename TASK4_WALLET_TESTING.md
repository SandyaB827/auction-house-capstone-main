# Task 4: Wallet Management - Testing Guide

## Implementation Summary

✅ **COMPLETED**: Task 4 - Wallet Management has been successfully implemented with all SRS requirements.

### Implemented Features

1. **WalletTransaction Entity**
   - Created WalletTransaction entity with transaction types and status
   - Added database migration and configuration
   - Linked to PortalUser with proper relationships

2. **Wallet DTOs Created**
   - `DepositRequest` - For wallet deposits with validation
   - `WithdrawRequest` - For wallet withdrawals with validation
   - `WalletResponse` - Comprehensive wallet dashboard response
   - `WalletTransactionResponse` - Transaction operation responses
   - `BlockAmountRequest` / `ReleaseAmountRequest` - For bid amount management

3. **Wallet Controller Implementation**
   - Full wallet operations as per SRS requirements
   - Transaction history tracking
   - Balance blocking/releasing for auction bids
   - Comprehensive error handling and logging

### API Endpoints Implemented

| Method | Endpoint | Description | SRS Reference |
|--------|----------|-------------|---------------|
| POST | `/api/wallet/deposit` | Deposit money to wallet | 4.2.1 |
| POST | `/api/wallet/withdraw` | Withdraw money from wallet | 4.2.2 |
| GET | `/api/wallet` | Get wallet dashboard | 4.2.3 |
| GET | `/api/wallet/transactions` | Get transaction history (paginated) | Additional |
| POST | `/api/wallet/block-amount` | Block amount for bids (internal) | Business Rule 1 |
| POST | `/api/wallet/release-amount` | Release blocked amount (internal) | Business Rule 1 |

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

#### 2. Deposit Money (SRS 4.2.1)
```json
POST /api/wallet/deposit
{
  "amount": 1000
}
```

**Expected Results:**
- Status: 200 OK
- Wallet balance increased by deposit amount
- Transaction record created with type "Deposit"
- Available balance calculated correctly

**Validation Tests:**
```json
// Test minimum amount
{
  "amount": 0.5
}
// Expected: 400 Bad Request (below minimum $1)

// Test maximum amount
{
  "amount": 1000000
}
// Expected: 400 Bad Request (above maximum $999,999)

// Test negative amount
{
  "amount": -100
}
// Expected: 400 Bad Request
```

#### 3. Withdraw Money (SRS 4.2.2)
```json
POST /api/wallet/withdraw
{
  "amount": 500
}
```

**Expected Results:**
- Status: 200 OK if sufficient available balance
- Wallet balance decreased by withdrawal amount
- Transaction record created with type "Withdrawal"
- Available balance calculated correctly (WalletBalance - BlockedAmount)

**Insufficient Funds Test:**
```json
// First deposit $100, then try to withdraw $200
POST /api/wallet/deposit
{
  "amount": 100
}

POST /api/wallet/withdraw
{
  "amount": 200
}
// Expected: 400 Bad Request with insufficient funds message
```

#### 4. Get Wallet Dashboard (SRS 4.2.3)
```json
GET /api/wallet
```

**Expected Results:**
- Returns current wallet balance
- Returns blocked amount
- Returns calculated available balance
- Returns list of blocked amount details (from active bids)
- Returns recent transaction history (last 20)

#### 5. Get Transaction History (Paginated)
```json
GET /api/wallet/transactions?page=1&pageSize=10
```

**Expected Results:**
- Returns paginated transaction history
- Includes pagination metadata (totalCount, page, pageSize, totalPages)
- Transactions ordered by date (newest first)
- Page size limited to maximum 100

#### 6. Block Amount for Bids (Internal API)
```json
POST /api/wallet/block-amount
{
  "userId": "user-id-here",
  "amount": 250,
  "auctionId": 1,
  "description": "Bid amount blocked for auction #1"
}
```

**Expected Results:**
- Status: 200 OK if sufficient available balance
- BlockedAmount increased by specified amount
- Transaction record created with type "BidBlocked"
- Available balance reduced accordingly

**Business Rule Validation:**
- Users can only block amounts for their own account (unless admin)
- Must have sufficient available balance
- Amount must be positive

#### 7. Release Blocked Amount (Internal API)
```json
POST /api/wallet/release-amount
{
  "userId": "user-id-here",
  "amount": 250,
  "auctionId": 1,
  "description": "Bid amount released for auction #1"
}
```

**Expected Results:**
- Status: 200 OK if sufficient blocked amount
- BlockedAmount decreased by specified amount
- Transaction record created with type "BidReleased"
- Available balance increased accordingly

### Business Rules Validation

#### SRS 4.2.1.1 - Deposit Restrictions
- ✅ Amount: 1-999,999 (positive integer values)
- ✅ Transaction logging for audit trail
- ✅ Immediate balance update

#### SRS 4.2.2.1 - Withdrawal Restrictions
- ✅ Amount: 1-999,999 (positive integer values)
- ✅ Available balance check (WalletBalance - BlockedAmount)
- ✅ Prevent withdrawal if insufficient funds
- ✅ Transaction logging for audit trail

#### Business Rule 1 - Bid Amount Blocking
- ✅ Block bid amount when user places highest bid
- ✅ Check available balance before blocking
- ✅ Release amount when outbid or auction ends
- ✅ Proper transaction tracking for all operations

### Authorization Testing

#### Test with Regular User
1. Register new user: `POST /api/auth/register`
2. Login and get token
3. Try to block/release amounts for another user
4. Expected: 403 Forbidden

#### Test with Admin
1. Login as admin
2. Should be able to block/release amounts for any user
3. Should have admin permissions for all operations

### Error Handling Testing

#### Test Insufficient Funds Scenarios
```json
// Deposit $100, try to withdraw $150
POST /api/wallet/deposit
{
  "amount": 100
}

POST /api/wallet/withdraw
{
  "amount": 150
}
// Expected: 400 Bad Request with specific error message
```

#### Test Insufficient Blocked Amount
```json
// Try to release more than blocked
POST /api/wallet/release-amount
{
  "userId": "user-id",
  "amount": 1000,
  "description": "Test release"
}
// Expected: 400 Bad Request if blocked amount < 1000
```

#### Test Unauthorized Access
```json
GET /api/wallet
```
Without Authorization header
Expected: 401 Unauthorized

### Integration Testing with Assets

#### Complete Workflow Test
1. **Setup User and Wallet**
   ```json
   POST /api/auth/register
   POST /api/auth/login
   POST /api/wallet/deposit {"amount": 5000}
   ```

2. **Create and List Asset**
   ```json
   POST /api/assets
   PATCH /api/assets/1/status {"status": "OpenToAuction"}
   ```

3. **Simulate Bid Process**
   ```json
   POST /api/wallet/block-amount {
     "userId": "user-id",
     "amount": 1000,
     "auctionId": 1,
     "description": "Bid for vintage guitar"
   }
   ```

4. **Check Wallet Status**
   ```json
   GET /api/wallet
   // Should show:
   // - WalletBalance: 5000
   // - BlockedAmount: 1000
   // - AvailableBalance: 4000
   ```

5. **Release Amount (Outbid Scenario)**
   ```json
   POST /api/wallet/release-amount {
     "userId": "user-id",
     "amount": 1000,
     "auctionId": 1,
     "description": "Outbid - amount released"
   }
   ```

## Implementation Details

### Key Features Implemented

1. **Transaction Tracking**
   - Complete audit trail for all wallet operations
   - Transaction types: Deposit, Withdrawal, BidBlocked, BidReleased, PaymentReceived, PaymentMade
   - Transaction status tracking: Pending, Completed, Failed, Cancelled

2. **Balance Management**
   - WalletBalance: Total money in wallet
   - BlockedAmount: Money blocked for active bids
   - AvailableBalance: WalletBalance - BlockedAmount (calculated property)

3. **Business Rule Enforcement**
   - Deposit/withdrawal amount limits ($1 - $999,999)
   - Available balance validation for withdrawals
   - Blocked amount validation for releases
   - Authorization checks for cross-user operations

4. **Comprehensive Logging**
   - All operations logged with user ID and amounts
   - Error logging for debugging
   - Transaction descriptions for audit trail

5. **Pagination Support**
   - Transaction history with configurable page size
   - Maximum page size limit (100)
   - Complete pagination metadata

### Database Schema
```sql
-- WalletTransactions table
WalletTransactions (
    Id INTEGER PRIMARY KEY,
    UserId TEXT NOT NULL,
    Type TEXT NOT NULL, -- Deposit, Withdrawal, BidBlocked, BidReleased, etc.
    Amount DECIMAL(18,2) NOT NULL,
    TransactionDate TEXT NOT NULL,
    Description TEXT(500),
    Status TEXT NOT NULL, -- Pending, Completed, Failed, Cancelled
    RelatedAuctionId INTEGER NULL,
    RelatedAssetId INTEGER NULL,
    FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id),
    FOREIGN KEY (RelatedAuctionId) REFERENCES Auctions(Id),
    FOREIGN KEY (RelatedAssetId) REFERENCES Assets(Id)
)

-- PortalUser wallet properties
AspNetUsers (
    ...existing Identity columns...
    WalletBalance DECIMAL(18,2) NOT NULL DEFAULT 0,
    BlockedAmount DECIMAL(18,2) NOT NULL DEFAULT 0
)
```

## Next Steps

Task 4 is now complete and ready for integration with:
- Task 5: Auctions Management (for bid amount blocking/releasing)
- Task 6: Dashboard (for wallet balance display)
- Task 7: Background Services (for transaction settlement)

The Wallet Management system provides a robust foundation for handling financial transactions in the auction house platform with proper validation, authorization, and audit trail as specified in the SRS document and Business Rule 1.

## Testing Checklist

- ✅ Deposit functionality with validation
- ✅ Withdrawal functionality with available balance check
- ✅ Wallet dashboard with balance and transaction history
- ✅ Amount blocking for bid management
- ✅ Amount releasing for bid management
- ✅ Transaction history with pagination
- ✅ Authorization and permission checks
- ✅ Error handling and validation
- ✅ Business rule enforcement
- ✅ Comprehensive logging and audit trail 