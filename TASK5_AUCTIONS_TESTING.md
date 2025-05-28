# Task 5: Auctions Management - Testing Guide

## Implementation Summary

✅ **COMPLETED**: Task 5 - Auctions Management has been successfully implemented with all SRS requirements.

### Implemented Features

1. **Auction DTOs Created**
   - `PostAuctionRequest` - For creating auctions with validation
   - `PlaceBidRequest` - For placing bids with validation
   - `AuctionResponse` - Comprehensive auction details with business logic
   - `BidResponse` - Bid placement results with wallet impact
   - `AuctionCloseResponse` - Auction closure results with transaction details

2. **Auctions Controller Implementation**
   - Full auction lifecycle management as per SRS requirements
   - Bid placement with wallet integration
   - Auction closure with asset transfer and payment processing
   - Comprehensive error handling and logging
   - Role-based authorization

3. **Business Logic Integration**
   - Asset status management during auction lifecycle
   - Wallet amount blocking/releasing for bids (Business Rule 1)
   - Asset ownership transfer on auction completion (Business Rule 2)
   - Payment processing between buyer and seller

### API Endpoints Implemented

| Method | Endpoint | Description | SRS Reference |
|--------|----------|-------------|---------------|
| POST | `/api/auctions` | Post an auction | 4.4.1 |
| POST | `/api/auctions/{id}/bid` | Place a bid | Bidding System |
| GET | `/api/auctions` | Get active auctions (sorted) | 4.3.1 |
| GET | `/api/auctions/{id}` | Get specific auction details | Additional |
| POST | `/api/auctions/{id}/close` | Close auction manually | Auction Closure |
| GET | `/api/auctions/my-auctions` | Get user's auctions (as seller) | Additional |
| GET | `/api/auctions/my-bids` | Get user's bid history | Additional |

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

#### 2. Setup Test Data
First, create assets and add wallet funds:

```json
// Create an asset
POST /api/assets
{
  "title": "Vintage Guitar Collection",
  "description": "A beautiful collection of vintage guitars from the 1960s",
  "retailValue": 5000
}

// Change asset status to OpenToAuction
PATCH /api/assets/1/status
{
  "status": "OpenToAuction"
}

// Add funds to wallet for bidding
POST /api/wallet/deposit
{
  "amount": 10000
}
```

#### 3. Post an Auction (SRS 4.4.1)
```json
POST /api/auctions
{
  "assetId": 1,
  "reservedPrice": 1000,
  "minimumBidIncrement": 100,
  "totalMinutesToExpiry": 60
}
```

**Expected Results:**
- Status: 201 Created
- Auction created with Live status
- Asset status changed to ClosedForAuction
- StartDate set to current UTC time
- Proper validation of all SRS restrictions

**Validation Tests:**
```json
// Test asset not in OpenToAuction status
{
  "assetId": 1,  // Asset in Draft status
  "reservedPrice": 1000,
  "minimumBidIncrement": 100,
  "totalMinutesToExpiry": 60
}
// Expected: 400 Bad Request

// Test reserve price limits (SRS 4.4.1.1)
{
  "assetId": 1,
  "reservedPrice": 10000,  // Above $9,999 limit
  "minimumBidIncrement": 100,
  "totalMinutesToExpiry": 60
}
// Expected: 400 Bad Request

// Test incremental value limits
{
  "assetId": 1,
  "reservedPrice": 1000,
  "minimumBidIncrement": 1000,  // Above $999 limit
  "totalMinutesToExpiry": 60
}
// Expected: 400 Bad Request

// Test expiration time limits
{
  "assetId": 1,
  "reservedPrice": 1000,
  "minimumBidIncrement": 100,
  "totalMinutesToExpiry": 15000  // Above 10,080 minutes (7 days)
}
// Expected: 400 Bad Request
```

#### 4. Place Bids
```json
POST /api/auctions/1/bid
{
  "bidAmount": 1000
}
```

**Expected Results:**
- Status: 200 OK
- Bid amount blocked in user's wallet
- Auction CurrentHighestBid updated
- BidHistory record created
- Previous highest bidder's amount released (if any)

**Business Logic Tests:**
```json
// Test minimum bid validation
POST /api/auctions/1/bid
{
  "bidAmount": 950  // Below reserve price of 1000
}
// Expected: 400 Bad Request

// Test incremental bid validation (after first bid)
POST /api/auctions/1/bid
{
  "bidAmount": 1050  // Less than 1000 + 100 increment
}
// Expected: 400 Bad Request

// Test seller cannot bid on own auction
// (Login as auction creator and try to bid)
// Expected: 400 Bad Request

// Test insufficient wallet funds
POST /api/auctions/1/bid
{
  "bidAmount": 50000  // More than available balance
}
// Expected: 400 Bad Request
```

#### 5. Get Active Auctions (SRS 4.3.1)
```json
GET /api/auctions?sortBy=expiry&page=1&pageSize=10
```

**Expected Results:**
- Returns live auctions sorted by expiry date
- User's highest bids appear first (SRS requirement)
- Includes NextCallPrice calculation
- Shows remaining time in minutes
- Pagination metadata included

**Sorting Options:**
```json
// Sort by expiry (default) - user bids first, then by nearest expiry
GET /api/auctions?sortBy=expiry

// Sort by user's active bids
GET /api/auctions?sortBy=bid

// Sort by creation date
GET /api/auctions?sortBy=created
```

#### 6. Get Auction Details
```json
GET /api/auctions/1
```

**Expected Results:**
- Complete auction information
- Asset details included
- Seller and current highest bidder information
- Full bid history with timestamps
- Permission flags (CanBid, CanClose)
- Calculated fields (NextCallPrice, RemainingTime, IsExpired)

#### 7. Close Auction
```json
POST /api/auctions/1/close
```

**Expected Results (With Bids):**
- Auction status changed to Expired
- Asset ownership transferred to highest bidder (Business Rule 2)
- Payment processed: winner's wallet debited, seller's wallet credited
- Blocked amount released and deducted from winner
- Asset status changed to OpenToAuction for new owner
- Transaction records created for both parties

**Expected Results (Without Bids):**
- Auction status changed to ExpiredWithoutBids
- Asset status returned to OpenToAuction (SRS 4.1.5)
- No payment processing
- No ownership transfer

#### 8. Get User's Auctions
```json
GET /api/auctions/my-auctions
```

**Expected Results:**
- Returns all auctions created by current user
- Ordered by creation date (newest first)
- Includes auction status and bid information

#### 9. Get User's Bid History
```json
GET /api/auctions/my-bids
```

**Expected Results:**
- Returns all bids placed by current user
- Shows which bids are currently highest
- Includes auction status and expiry information
- Ordered by bid date (newest first)

### Business Rules Validation

#### SRS 4.4.1.1 - Post Auction Restrictions
- ✅ Asset must be in OpenToAuction status
- ✅ Reserve price: $1-$9,999 (non-zero positive integer)
- ✅ Incremental value: $1-$999 (non-zero positive integer)
- ✅ Expiration time: 1-10,080 minutes (7 days maximum)
- ✅ Only asset owner can auction (unless admin)

#### Business Rule 1 - Bid Amount Blocking
- ✅ Block bid amount when user places highest bid
- ✅ Release previous highest bidder's amount when outbid
- ✅ Check available balance before allowing bid
- ✅ Proper transaction tracking for all operations

#### Business Rule 2 - Asset Ownership Transfer
- ✅ Transfer asset to highest bidder on auction completion
- ✅ New owner can auction the asset again
- ✅ Asset status management throughout lifecycle

#### SRS 4.3.1 - Auction Listing Requirements
- ✅ Show all open auctions sorted by nearest expiry
- ✅ User's active bids appear at top
- ✅ Display current highest bid and next call price
- ✅ Show auction status and remaining time

### Authorization Testing

#### Test Role-Based Access
1. **Seller Role**: Can post auctions, close own auctions
2. **Bidder Role**: Can place bids, view bid history
3. **Admin Role**: Can manage all auctions and bids
4. **User Role**: Basic access to view auctions

#### Test Ownership Validation
- Users can only auction their own assets
- Users can only close their own auctions
- Sellers cannot bid on their own auctions

### Error Handling Testing

#### Test Auction Expiry
```json
// Try to bid on expired auction
POST /api/auctions/1/bid
{
  "bidAmount": 2000
}
// Expected: 400 Bad Request if auction expired
```

#### Test Asset Already in Auction
```json
// Try to auction asset that's already in active auction
POST /api/auctions
{
  "assetId": 1,  // Asset already in auction
  "reservedPrice": 1000,
  "minimumBidIncrement": 100,
  "totalMinutesToExpiry": 60
}
// Expected: 400 Bad Request
```

### Integration Testing Workflow

#### Complete Auction Lifecycle Test
1. **Setup Users and Assets**
   ```json
   // Register seller and bidders
   POST /api/auth/register (seller)
   POST /api/auth/register (bidder1)
   POST /api/auth/register (bidder2)
   
   // Add wallet funds
   POST /api/wallet/deposit {"amount": 5000} (each user)
   
   // Create and prepare asset
   POST /api/assets (seller)
   PATCH /api/assets/1/status {"status": "OpenToAuction"}
   ```

2. **Create Auction**
   ```json
   POST /api/auctions {
     "assetId": 1,
     "reservedPrice": 1000,
     "minimumBidIncrement": 100,
     "totalMinutesToExpiry": 60
   }
   ```

3. **Bidding Process**
   ```json
   // Bidder1 places first bid
   POST /api/auctions/1/bid {"bidAmount": 1000}
   
   // Check wallet: $1000 should be blocked
   GET /api/wallet
   
   // Bidder2 outbids
   POST /api/auctions/1/bid {"bidAmount": 1100}
   
   // Check Bidder1 wallet: $1000 should be released
   // Check Bidder2 wallet: $1100 should be blocked
   ```

4. **Auction Completion**
   ```json
   // Close auction
   POST /api/auctions/1/close
   
   // Verify results:
   // - Asset ownership transferred to Bidder2
   // - Bidder2 wallet: -$1100 (payment made)
   // - Seller wallet: +$1100 (payment received)
   // - Asset status: OpenToAuction (new owner can auction)
   ```

## Implementation Details

### Key Features Implemented

1. **Complete Auction Lifecycle**
   - Auction creation with asset validation
   - Bid placement with wallet integration
   - Automatic amount blocking/releasing
   - Auction closure with payment processing
   - Asset ownership transfer

2. **Business Logic Enforcement**
   - SRS validation rules for all auction parameters
   - Bid amount validation with incremental requirements
   - Wallet balance checks before bid placement
   - Asset status management throughout lifecycle

3. **Advanced Sorting and Filtering**
   - User's active bids prioritized in listings
   - Multiple sorting options (expiry, bid, created)
   - Pagination support for large datasets
   - Real-time remaining time calculations

4. **Comprehensive Transaction Tracking**
   - Bid history with timestamps and bidder information
   - Wallet transactions for all bid-related operations
   - Payment transactions for auction completions
   - Complete audit trail for all operations

5. **Permission-Based Responses**
   - `CanBid`: Based on auction status, expiry, and ownership
   - `CanClose`: Based on ownership and admin rights
   - Dynamic permission calculation for UI guidance

### Database Integration

- **Auction Entity**: Complete lifecycle management
- **BidHistory Entity**: Full bid tracking with relationships
- **WalletTransaction Entity**: Integrated payment processing
- **Asset Entity**: Status management and ownership transfer

## Next Steps

Task 5 is now complete and ready for integration with:
- Task 6: Dashboard (for auction listings and user bid display)
- Task 7: Background Services (for automatic auction expiry processing)

The Auctions Management system provides a complete auction platform with proper validation, authorization, business rule enforcement, and financial transaction processing as specified in the SRS document and business rules.

## Testing Checklist

- ✅ Auction creation with SRS validation
- ✅ Bid placement with wallet integration
- ✅ Auction listing with proper sorting (SRS 4.3.1)
- ✅ Auction closure with payment processing
- ✅ Asset ownership transfer (Business Rule 2)
- ✅ Bid amount blocking/releasing (Business Rule 1)
- ✅ Role-based authorization and permissions
- ✅ Error handling and validation
- ✅ Complete auction lifecycle testing
- ✅ Integration with Assets and Wallet systems 