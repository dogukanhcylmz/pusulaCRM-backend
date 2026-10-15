# BACKLOG

İlk inceleme (2026-10-15) bulguları. Durum: `[ ]` açık, `[x]` bitti. Bitirince raporu `docs/reports/` altına yaz.

## Hatalar
- [ ] B1 `Services/Ai/ChatService.cs:46` — `m.Role is "user" or "assistant" && !IsNullOrWhiteSpace(...)` önceliği: boş içerik kontrolü yalnız "assistant" koluna bağlı. Parantezle düzelt.
- [ ] B2 `Services/Ai/ChatService.cs:127` — `ex is A or B && !ct.IsCancellationRequested` aynı öncelik sorunu.
- [ ] B3 `ChatService.cs:78-109` — aynı turda birden çok `tool_call` gelirse yazma aracında `return` ile diğer çağrılar yanıtsız kalır.
- [ ] B4 `QuoteService.NextNumberAsync` — eşzamanlı isteklerde aynı numara → unique index ihlali (500). Retry/sayaç.
- [ ] B5 `PendingActionStore` bellek içi — restart/çoklu instance'ta bekleyen onaylar kaybolur.

## Güvenlik / üretim
- [ ] G1 Kimlik doğrulama yok; kullanıcı `appsettings` içinde sabit "Demo Kullanıcı".
- [ ] G2 CORS origin'i sabit (`localhost:5173`) → yapılandırmaya taşı.
- [ ] G3 Onay endpoint'i (`/chat/actions/{id}/confirm`) kullanıcıya bağlı değil.
- [ ] G4 Exception handler: `DbUpdateException` vb. için anlamlı cevap; `ArgumentException` mesajı sızıyor.
- [ ] G5 `EnsureCreated` → EF migration'a geç.

## Performans
- [ ] P1 `CustomerService.GetAllAsync/FindMatchesAsync` tüm müşterileri belleğe çekiyor.
- [ ] P2 `DashboardService` tüm teklifleri `Include` ile çekip bellekte topluyor → SQL aggregate.
- [ ] P3 `CrmToolbox.PrepareQuoteStatus` tek teklif için tümünü çekiyor → numaraya göre sorgu.
- [ ] P4 Liste endpoint'lerinde sayfalama yok.

## Küçük / eksik
- [ ] K1 `ActivitiesController.Create` — `Created("api/activities", …)` anlamsız konum URL'si.
- [ ] K2 `ActivitySaveDto.DueDate` UTC/yerel sözleşmesi belirsiz.
- [ ] K3 Test projesi yok (öncelik: teklif hesapları, CrmToolbox, ChatService).
- [ ] K4 `appsettings.json` model adı `gemini-3.8-flash` — geçerliliğini doğrula.
- [ ] K5 Teklif düzenleme (kalem/başlık `PUT`) yok; yalnız durum değişiyor.

## Okunmadı (inceleme eksiği)
`Data/SeedData.cs`, `Data/UtcDateTimeConverter.cs`, `Controllers/MeController.cs`, `Controllers/DashboardController.cs`

## Önerilen sıra
B1, B2 → K3 (test altyapısı) → B4, P3 → G1-G3 → geri kalanı
