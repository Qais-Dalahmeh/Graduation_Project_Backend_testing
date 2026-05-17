# Test Coverage Analysis — Mall Loyalty Backend

## Summary

| Category | Before Quality Tests | After Quality Tests |
|---|---|---|
| Total unit tests | 10 | 48 |
| Services with zero coverage | 2 (Offers, Announcements) | 0 |
| Services partially covered | 3 | 5 |
| Services fully covered | 0 | 0 (auth + rewards close) |

---

## Existing Tests (10 tests)

### ChatbotService — 3 tests
| Test | What it verifies |
|---|---|
| `AskAsync_UsesOnlyMsgAndStaticMallInfo` | Sends correct Authorization header, model, system prompt with mall_info, user message |
| `AskAsync_AcceptsMessegeAlias` | `Messege` field alias is forwarded correctly to the AI request body |
| `GetHistoryAsync_ReturnsEmptyListBecauseChatbotDoesNotUseDatabase` | Hardcoded empty list — no DB persistence implemented |

### RewardsService — 2 tests
| Test | What it verifies |
|---|---|
| `GetReceiptDetailsForUserAsync_NonOwnerCustomer_ThrowsForbidden` | A user cannot read another user's receipt |
| `GetMyReceiptsAsync_ReturnsOnlyCurrentUsersReceipts` | Pagination query is scoped to requesting user only |

### DashboardService — 1 test
| Test | What it verifies |
|---|---|
| `GetSummaryAsync_StoreScopedManager_OnlyCountsAssignedStoreTransactions` | Store-scoped manager's summary counts only their assigned store, not others in same mall |

### AuthService — 2 tests
| Test | What it verifies |
|---|---|
| `RegisterAsync_WithManagerId_CreatesManagerLinkedUserProfile` | Manager registration links profile ID to Manager entity |
| `ManagerQuickLoginAsync_WithManagerId_CreatesLinkedUserProfileAndSession` | Quick login creates UserProfile with placeholder phone |

### StoresService — 2 tests
| Test | What it verifies |
|---|---|
| `CreateStoreAsync_MallWideManager_CreatesStoreAndCategories` | Mall-wide manager can create store with categories; name is trimmed |
| `CreateStoreAsync_StoreScopedManager_ThrowsForbidden` | Store-scoped manager is blocked from creating stores |

---

## New Quality Tests (38 tests)

### OffersService — 8 tests (`OffersServiceTests.cs`)
| Test | What it verifies |
|---|---|
| `GetVisibleOffersAsync_ReturnsOnlyActiveOffersWithinTimeWindow` | Expired, future, and inactive offers are hidden from users |
| `GetVisibleOffersAsync_ExcludesOffersFromOtherMalls` | User only sees offers from their own mall |
| `CreateOfferAsync_MallWideManager_CreatesOfferSuccessfully` | Mall-wide manager creates offer; title whitespace is trimmed |
| `CreateOfferAsync_EndDateBeforeStartDate_ThrowsValidationException` | End < Start → `ApiValidationException` with code `INVALID_DATE_RANGE` |
| `CreateOfferAsync_StoreScopedManager_UnassignedStore_ThrowsForbidden` | Store manager cannot create offer for a store outside their scope |
| `CreateOfferAsync_RegularUser_ThrowsForbidden` | Non-manager role is blocked from creating offers |
| `SetOfferStatusAsync_DeactivatesActiveOffer` | Manager can toggle offer to inactive; change persists in DB |
| `DeleteOfferAsync_MallWideManager_RemovesOfferFromDatabase` | Deleted offer is removed from DB entirely |

### AnnouncementsService — 9 tests (`AnnouncementsServiceTests.cs`)
| Test | What it verifies |
|---|---|
| `GetVisibleAnnouncementsAsync_ReturnsOnlyActiveAnnouncementsWithinTimeWindow` | Expired, future, and inactive announcements are hidden |
| `GetVisibleAnnouncementsAsync_PinnedAnnouncementsAppearFirst` | Pinned announcements sort before unpinned regardless of date |
| `GetVisibleAnnouncementsAsync_HighPriorityBeforeNormalWhenBothUnpinned` | `priority = "high"` sorts before `priority = "normal"` |
| `CreateAnnouncementAsync_ValidRequest_AppliesDefaultTypeAndPriority` | Omitted type defaults to `"general"`, omitted priority to `"normal"` |
| `CreateAnnouncementAsync_EndDateBeforeStartDate_ThrowsValidation` | End < Start → `ApiValidationException` |
| `CreateAnnouncementAsync_StoreScopedManager_WithoutStoreId_ThrowsForbidden` | Store manager cannot create a mall-wide announcement (no storeId) |
| `SetAnnouncementPinAsync_PinsExistingAnnouncement` | `IsPinned` is set to true and persisted |
| `DeleteAnnouncementAsync_RemovesAnnouncementFromDatabase` | Deleted announcement is gone from DB |
| `GetManagedAnnouncementsAsync_RegularUser_ThrowsForbidden` | Non-manager is blocked from the managed list endpoint |

### RewardsService (Coupons + Transactions) — 11 tests (`RewardsServiceCouponTests.cs`)
| Test | What it verifies |
|---|---|
| `RedeemCouponAsync_ValidCouponWithCostPoints_DeductsFromUserBalance` | 500 pts - 100 pt coupon = 400 pts; serial is 8 digits; `IsRedeemed = false` initially |
| `RedeemCouponAsync_FreeCoupon_DoesNotChangePoints` | `CostPoint = null` → user balance unchanged |
| `RedeemCouponAsync_NotEnoughPoints_Throws` | User has 50 pts, coupon costs 200 → `InvalidOperationException` |
| `RedeemCouponAsync_InactiveCoupon_Throws` | `IsActive = false` → blocked |
| `RedeemCouponAsync_ExpiredCoupon_Throws` | `EndAt` in past → blocked |
| `RedeemCouponAsync_FutureCoupon_Throws` | `StartAt` in future → blocked |
| `RedeemCouponBySerialAsync_ValidSerial_MarksAsRedeemed` | `IsRedeemed` becomes true, persisted in DB |
| `RedeemCouponBySerialAsync_AlreadyRedeemed_Throws` | Double redemption → `"already redeemed"` message |
| `RedeemCouponBySerialAsync_NonExistentSerial_Throws` | Unknown serial → `"serial not found"` message |
| `ProcessTransactionAsync_ValidPurchase_AwardsPointsAndRecordsTransaction` | 50.00 price → 5000 points; transaction saved; user balance updated |
| `ProcessTransactionAsync_DuplicateReceiptId_Throws` | Same receipt ID twice → blocked |

### AuthService (Login / Logout / Validation) — 10 tests (`AuthServiceLoginTests.cs`)
| Test | What it verifies |
|---|---|
| `LoginAsync_ValidCredentials_ReturnsSessionWithCorrectUser` | Correct phone + password + mall → session issued |
| `LoginAsync_WrongPassword_ThrowsUnauthorized` | Wrong password → `AuthUnauthorizedException` |
| `LoginAsync_NonExistentPhone_ThrowsUnauthorized` | Unknown phone → `AuthUnauthorizedException` |
| `LoginAsync_CorrectPhoneWrongMall_ThrowsUnauthorized` | Phone exists but in different mall → blocked |
| `RegisterAsync_DuplicatePhone_SameMall_ThrowsConflict` | Same phone + same mall → `AuthConflictException` |
| `RegisterAsync_SamePhone_DifferentMall_ThrowsConflict` | Same phone in two malls → `AuthConflictException` |
| `RegisterAsync_EmptyName_ThrowsValidation` | Whitespace-only name → `AuthValidationException` |
| `RegisterAsync_EmptyPassword_ThrowsValidation` | Empty password → `AuthValidationException` |
| `LogoutAsync_ValidSession_DeletesSession` | Session is deleted; `UserSessions` table is empty |
| `LogoutAsync_NonExistentSession_ThrowsNotFound` | Fake session ID → `AuthNotFoundException` |
| `LoginAsync_CreatesNewSession_ReplacingPreviousOne` | Second login replaces old session; only 1 active session at a time |

---

## Gaps Still Not Covered by Unit Tests

| Service | Method | Reason not tested |
|---|---|---|
| `DashboardService` | `GetSalesAsync`, `GetPointsAsync`, `GetCouponsAsync`, `GetActivityAsync` | Complex LINQ with grouped projections; better covered by E2E |
| `StoresService` | `UpdateStoreAsync`, `GetVisibleStoresAsync` | CRUD paths; covered by E2E Playwright tests |
| `RewardsService` | `GetUserCouponsViewAsync`, `GetCouponDetailsAsync` | Simple DB reads; covered by E2E |
| `ChatbotService` | Retry logic | Requires an HTTP handler that simulates 429/5xx responses — integration concern |
| `ChatbotService` | `GetHistoryAsync` saving to DB | Known gap: service never injects `AppDbContext`; history is hardcoded empty |

---

## How to Run

```bash
# All tests (existing + quality)
dotnet test Graduation_Project_Backend.Tests/

# Quality tests only
dotnet test Graduation_Project_Backend.Tests/ --filter "FullyQualifiedName~QualityTests"

# One specific suite
dotnet test Graduation_Project_Backend.Tests/ --filter "FullyQualifiedName~OffersServiceTests"
```
