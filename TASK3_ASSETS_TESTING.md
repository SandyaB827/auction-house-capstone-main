# Task 3: Assets Management - Testing Guide

## Implementation Summary

✅ **COMPLETED**: Task 3 - Assets Management has been successfully implemented with all SRS requirements.

### Implemented Features

1. **Asset Entity Enhancement**
   - Added `CreatedDate` property to Asset entity
   - Created and applied database migration

2. **Asset DTOs Created**
   - `CreateAssetRequest` - For asset creation with validation
   - `UpdateAssetRequest` - For asset updates with validation
   - `ChangeAssetStatusRequest` - For status changes
   - `AssetResponse` - Comprehensive asset response with permissions
   - `AssetListResponse` - For listing assets

3. **Assets Controller Implementation**
   - Full CRUD operations as per SRS requirements
   - Role-based authorization
   - Business rule enforcement
   - Comprehensive error handling and logging

### API Endpoints Implemented

| Method | Endpoint | Description | SRS Reference |
|--------|----------|-------------|---------------|
| POST | `/api/assets` | Create new asset | 4.1.1 |
| PUT | `/api/assets/{id}` | Update asset | 4.1.2 |
| PATCH | `/api/assets/{id}/status` | Change asset status | 4.1.3 |
| DELETE | `/api/assets/{id}` | Delete asset | 4.1.7 |
| GET | `/api/assets` | Get user's assets | 4.1.8 |
| GET | `/api/assets/{id}` | Get specific asset | Additional |
| GET | `/api/assets/available` | Get assets available for auction | Additional |

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

#### 2. Create Asset (SRS 4.1.1)
```json
POST /api/assets
{
  "title": "Vintage Guitar Collection",
  "description": "A beautiful collection of vintage guitars from the 1960s",
  "retailValue": 5000
}
```

**Expected Results:**
- Status: 201 Created
- Asset created with `Draft` status
- Title cleaned (special chars removed, spaces normalized)
- CreatedDate set to current UTC time

**Validation Tests:**
```json
// Test title too short
{
  "title": "Short",
  "description": "Valid description here",
  "retailValue": 1000
}
// Expected: 400 Bad Request

// Test title with special characters
{
  "title": "Guitar!@#$%^&*()",
  "description": "Valid description here", 
  "retailValue": 1000
}
// Expected: Title cleaned to "Guitar"

// Test negative retail value
{
  "title": "Valid Title Here",
  "description": "Valid description here",
  "retailValue": -100
}
// Expected: 400 Bad Request
```

#### 3. Update Asset (SRS 4.1.2)
```json
PUT /api/assets/1
{
  "title": "Updated Vintage Guitar Collection",
  "description": "Updated description with more details",
  "retailValue": 6000
}
```

**Expected Results:**
- Status: 200 OK if asset is in Draft status
- Status: 400 Bad Request if asset is not in Draft status

#### 4. Change Asset Status (SRS 4.1.3)
```json
PATCH /api/assets/1/status
{
  "status": "OpenToAuction"
}
```

**Valid Status Transitions:**
- Draft → OpenToAuction ✅
- OpenToAuction → ClosedForAuction ✅
- ClosedForAuction → OpenToAuction ✅
- Any other transition ❌

#### 5. Delete Asset (SRS 4.1.7)
```json
DELETE /api/assets/1
```

**Expected Results:**
- Status: 200 OK if asset is Draft or OpenToAuction
- Status: 400 Bad Request if asset is ClosedForAuction
- Status: 404 Not Found if asset doesn't exist
- Status: 403 Forbidden if user doesn't own asset (non-admin)

#### 6. Get User Assets (SRS 4.1.8)
```json
GET /api/assets
```

**Expected Results:**
- Returns all assets owned by current user
- Admins see all assets in system
- Assets ordered by CreatedDate (newest first)
- Includes permission flags (CanEdit, CanDelete, CanChangeStatus)

#### 7. Get Available Assets
```json
GET /api/assets/available
```

**Expected Results:**
- Returns only assets with OpenToAuction status
- Available to all authenticated users
- Useful for auction browsing

### Business Rules Validation

#### SRS 4.1.1.1 - Asset Creation Restrictions
- ✅ Title: 10-150 characters, no special chars, spaces trimmed
- ✅ Description: 10-1000 characters, special chars allowed
- ✅ RetailValue: positive integer
- ✅ Default status: Draft

#### SRS 4.1.2.1 - Asset Update Restrictions
- ✅ Only Draft status assets can be updated
- ✅ Same validation as creation
- ✅ Only owner or admin can update

#### SRS 4.1.7.1 - Asset Deletion Restrictions
- ✅ Only Draft or OpenToAuction status assets can be deleted
- ✅ Only owner or admin can delete

### Authorization Testing

#### Test with Regular User
1. Register new user: `POST /api/auth/register`
2. Login and get token
3. Try to access another user's assets
4. Expected: 403 Forbidden

#### Test with Admin
1. Login as admin
2. Should be able to view/manage all assets
3. Should have admin permissions in response

### Error Handling Testing

#### Test Invalid Asset ID
```json
GET /api/assets/999999
```
Expected: 404 Not Found

#### Test Unauthorized Access
```json
GET /api/assets
```
Without Authorization header
Expected: 401 Unauthorized

#### Test Invalid Status Transition
```json
PATCH /api/assets/1/status
{
  "status": "Draft"
}
```
When asset is in OpenToAuction status
Expected: 400 Bad Request

## Implementation Details

### Key Features Implemented

1. **Title Cleaning Logic**
   - Removes special characters using regex
   - Normalizes multiple spaces to single space
   - Trims leading/trailing spaces

2. **Status Transition Validation**
   - Enforces valid state machine transitions
   - Prevents invalid status changes

3. **Permission-Based Responses**
   - `CanEdit`: Only Draft status assets by owner/admin
   - `CanDelete`: Only Draft/OpenToAuction by owner/admin
   - `CanChangeStatus`: Based on current status and ownership

4. **Comprehensive Logging**
   - All operations logged with user ID and asset ID
   - Error logging for debugging

5. **Role-Based Authorization**
   - Uses existing authorization policies
   - Admins have elevated permissions
   - Users can only manage their own assets

### Database Schema
```sql
-- Asset table with new CreatedDate column
Assets (
    Id INTEGER PRIMARY KEY,
    OwnerId TEXT NOT NULL,
    Title TEXT(150) NOT NULL,
    Description TEXT(1000) NOT NULL,
    RetailValue INTEGER NOT NULL,
    Status TEXT NOT NULL,
    CreatedDate TEXT NOT NULL,
    FOREIGN KEY (OwnerId) REFERENCES AspNetUsers(Id)
)
```

## Next Steps

Task 3 is now complete and ready for integration with:
- Task 4: Wallet Management (for user balance tracking)
- Task 5: Auctions Management (for posting assets to auction)

The Assets API provides a solid foundation for the auction system with proper validation, authorization, and business rule enforcement as specified in the SRS document. 