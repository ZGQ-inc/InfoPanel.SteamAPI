# Phase 3 Quick Reference Card

**🎯 Current Mission**: Migrate ~150+ legacy logging calls gracefully

---

## 📋 QUICK SESSION CHECKLIST

**Before**:
- [ ] `git status` - ensure clean working directory
- [ ] Read service code - understand context
- [ ] Identify all logging calls - use search

**During**:
- [ ] One method at a time - methodical approach
- [ ] Build every 3-5 changes - catch errors early
- [ ] Use structured data objects - not string concatenation

**After**:
- [ ] `dotnet build -c Release` - verify build
- [ ] `git add` + `git commit` - save progress
- [ ] Update progress docs - track completion

---

## 🔧 LOGGING PATTERNS CHEAT SHEET

```csharp
// ✅ GOOD: Structured Info
_enhancedLogger?.LogInfo("Service", "Action completed", new {
    Count = items.Count,
    Duration = $"{ms}ms",
    Success = true
});

// ✅ GOOD: Operation Pairing
var id = _enhancedLogger?.LogOperationStart("Service", "Operation", params);
// ... work ...
_enhancedLogger?.LogOperationEnd("Service", "Operation", id, duration, success, result);

// ✅ GOOD: Error with Context
_enhancedLogger?.LogError("Service", "Failed", ex, new { 
    CorrelationId = id,
    Context = data 
});

// ❌ BAD: String concatenation
_enhancedLogger?.LogInfo("Service", $"Processed {count} items");

// ❌ BAD: No structured data
_enhancedLogger?.LogDebug("Service", "Completed");

// ❌ BAD: Sensitive data
_enhancedLogger?.LogInfo("Service", "API", new { ApiKey = key });
```

---

## 📊 SESSION PRIORITY ORDER

1. **Session 1**: SocialDataService (13 calls, 25min) ✅ Complete (f819b2a)
2. **Session 2**: LibraryDataService (13 calls, 20min) ✅ Complete (97c45f3)
3. **Session 3**: GameStatsService (11 calls, 15min) ✅ Complete (d81cf5f)
4. **Session 4**: SessionTrackingService (31 calls, 35min) ✅ Complete (5d65345)
5. **Session 5**: SensorManagementService (53 calls, 45min) ✅ Complete (98e8412)
6. **Session 6**: ConfigurationService (11 calls, 20min) ✅ Complete (a91db33)
7. **Session 7A**: SteamApiService Core (~30-40 calls, 60min) ⏳
8. **Session 7B**: SteamApiService Extended (~30-40 calls, 60min) ⏳
9. **Session 9**: SteamTokenService (~30 calls, 45min) ⏳

**Total**: ~230+ calls (revised after S4, S5, & S6 discoveries), ~7-10 hours over 3-5 days
**Completed**: 132 calls (57% of work, 96-99% of log volume!)

---

## 🎯 SUCCESS CRITERIA PER SESSION

- ✅ Zero new warnings
- ✅ All logging structured
- ✅ Sensitive data redacted
- ✅ Git commit created
- ✅ Progress docs updated

---

## 🚨 WHEN TO STOP & ROLLBACK

- ❌ Build fails after multiple attempts
- ❌ New warnings introduced you can't fix
- ❌ File corruption detected
- ❌ Lost track of changes

**Rollback**: `git reset --hard HEAD` or `git revert <commit>`

---

## 📈 TRACK YOUR PROGRESS

| Session | Status | Commit Hash | Date |
|---------|--------|-------------|------|
| 1 - Social | ✅ | f819b2a | 2025-11-11 |
| 2 - Library | ✅ | 97c45f3 | 2025-11-11 |
| 3 - GameStats | ✅ | d81cf5f | 2025-11-11 |
| 4 - Session | ⏳ | - | - |
| 5 - Sensor | ⏳ | - | - |
| 6 - Config | ⏳ | - | - |
| 7A - API Core | ⏳ | - | - |
| 7B - API Ext | ⏳ | - | - |
| 9 - Token | ⏳ | - | - |

---

**Start Command**: 
```bash
git checkout -b phase3-session-1-social
# Begin work on SocialDataService
```

---
