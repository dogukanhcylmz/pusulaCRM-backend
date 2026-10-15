# Raporlama Sistemi

Her anlamlı iş (geliştirme, test, inceleme) için bir rapor yazılır. Amaç: sonraki oturumda bağlamı hızlı geri kurmak. Git olmadığı için bu klasör aynı zamanda değişiklik geçmişidir.

## Adlandırma
`YYYY-MM-DD_<rol>_<konu>.md` — rol: `dev` | `test` | `review`
Örnek: `2026-10-15_dev_chat-oncelik-hatasi.md`

## Şablon

```markdown
# <Başlık>
- Tarih: YYYY-MM-DD   - Rol: dev|test|review   - Durum: tamamlandı | kısmi | engelli

## Amaç
Neden yapıldı (1-2 cümle).

## Yapılanlar
- dosya:satır — ne değişti

## Doğrulama
- Çalıştırılan komut → sonuç (geçti/kaldı, çıktı özeti)
- Yapılamayan doğrulamalar ve nedeni

## Bulgular / Riskler
- Yeni fark edilenler (BACKLOG'a eklendi mi?)

## Sonraki adım
- ...
```

## Kurallar
- Doğrulanmayan şey "çalışıyor" diye yazılmaz.
- Başarısız test/derleme çıktısı özetlenir, gizlenmez.
- Rapor sonrası `docs/BACKLOG.md` güncellenir.
- Raporlar silinmez; yanlışsa altına düzeltme notu eklenir.

## Rapor listesi
| Tarih | Rol | Konu | Dosya |
|---|---|---|---|
| 2026-10-15 | review | İlk proje incelemesi | (bkz. `../BACKLOG.md`) |
