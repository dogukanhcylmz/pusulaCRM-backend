# digitestCRM API — Proje Notları

Her oturumda otomatik yüklenir. Kısa tut; ayrıntı `docs/` altında.

## Teknoloji
- .NET 8, ASP.NET Core Web API, EF Core 8 + SQLite (`digitestcrm.db`, `EnsureCreated`, migration YOK)
- Swagger (sadece Development), CORS: `http://localhost:5173` (sabit)
- Git deposu (branch `main`, origin: github.com/dogukanhcylmz/digitestCRM-backend). Büyük değişiklikten önce kısa plan yaz; commit'ler kullanıcıda.

## Katmanlar
- `Controllers/` ince; iş kuralı `Services/` içinde. Hata sözleşmesi `Program.cs` içindeki exception handler: `KeyNotFoundException`→404, `ArgumentException`→400, diğer→500.
- `Models/` (Customer, Quote+QuoteItem, Activity), `Dtos/Dtos.cs` (hepsi tek dosya), `Data/AppDbContext.cs`.
- Tarihler DB'de UTC. `LocalClock` (Europe/Istanbul) ile yerel saate çevrilir.
- Arama: `TextMatch` Türkçe karakter duyarlı; bellekte filtreler.

## Yapay zekâ asistanı (`Services/Ai/`)
- `ChatService`: döngü (max 6 adım). Okuma aracı hemen çalışır; yazma aracı `PendingActionStore`'a (IMemoryCache, 15 dk) önizleme olarak düşer, `ConfirmAsync` ile gerçekten yazılır.
- `CrmToolbox`: araç şemaları + Prepare/Execute ayrımı. Yeni yazma aracı eklerken: `WriteTools` kümesi, `Definitions`, `PrepareWriteAsync`, `ExecuteWriteAsync`, `PromptBuilder` şablonu — hepsi güncellenmeli.
- `FallbackAssistant`: API anahtarı yoksa kural tabanlı mod. `AiClient`: OpenAI uyumlu HTTP istemcisi.

## Kurallar (kullanıcıyla kararlaştırıldı)
- Dil: kod yorumları ve kullanıcıya dönen mesajlar Türkçe; mevcut üslubu koru.
- Yazma işlemleri asistanda her zaman onaydan geçer; bunu atlatan değişiklik yapma.
- Mevcut kod stiline uy (primary-ctor değil, açık ctor; `record` DTO; `async` + `AsNoTracking`).
- **Commit ve push'u HER ZAMAN kullanıcı yapar.** `git add/commit/push` çalıştırma. İş bitince sohbette "Önerilen commit mesajı" ver (Türkçe, kısa başlık + madde listesi). Salt okunur git komutları serbest.
- Kapsam dışına taşma.

## Çalışma düzeni
- Geliştirme: `.claude/agents/developer.md`  · Test: `.claude/agents/tester.md`
- Rapor sistemi: `docs/reports/README.md` (şablon + adlandırma)
- Açık bulgular / yapılacaklar: `docs/BACKLOG.md` ← işe başlamadan önce oku, bitince güncelle
- Sürüm/Derleme kontrolü: `dotnet build` (proje klasöründe). Test projesi henüz yok.
