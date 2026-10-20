# Macro Studio — Ana Prompt (Master Prompt)

> **Bu dosya ne?** Macro Studio'yu sıfırdan yazacak yapay zekâya (veya geliştiriciye) verilecek tek kaynak belgedir.
> Hem insanın hem yapay zekânın okuyabileceği şekilde yazıldı: açıklamalar Türkçe; **tüm kod, dosya adları, sınıf adları, değişkenler, yorum satırları ve log mesajları İngilizcedir.**
>
> Güven etiketleri: **[Kesin]** = doğrulanmış teknik gerçek, **[Muhtemel]** = güçlü çıkarım, **[Tahmin]** = boşluk dolduran karar, gerekirse değiştirilebilir.

---

## 0. Kodlamadan önce bilinmesi gereken gerçekler

Bu maddeler tasarımı belirler. Yapay zekâ bunları ihlal eden bir çözüm önermemelidir.

1. **Fare tekerleğiyle kaydırma her döngüde aynı mesafeyi kaydırmaz.** [Kesin]
   Bir tekerlek tıkının kaç satır kaydıracağı Windows ayarına, Excel yakınlaştırmasına, satır yüksekliğine ve pencerenin o anki konumuna bağlıdır. Döngü uzadıkça kayma birikir ve makro **sessizce yanlış hücreyi** kopyalar; yanlış veri hedef programa girer.
   → **Karar:** Excel'de "bir sonraki satıra geç" işlemi tekerlekle değil, **↓ (Aşağı ok) tuşuyla** yapılır; tam olarak bir hücre ilerler. Kayıt sırasında Excel penceresinde tekerlek algılanırsa uygulama kullanıcıya "Bunu ↓ tuşuna çevireyim mi?" diye sorar. Tekerlek adımı yine desteklenir ama düzenleyicide uyarı ikonu taşır.

2. **Alt+Tab her zaman aynı pencereye götürmez.** [Kesin]
   Alt+Tab "en son kullanılan pencereye" gider. Arada bir bildirim, Teams mesajı veya açılan bir pencere odağı alırsa makro yanlış programa yapıştırır.
   → **Karar:** Kayıtta Alt+Tab yakalanır ama adım olarak **"Pencereyi öne getir: <program + başlık>"** şeklinde saklanır. Oynatmada pencere adıyla bulunur ve öne getirilir. Pencere bulunamazsa makro durur, tahmin yürütmez.

3. **Excel'den tek hücre kopyalandığında panoya sonunda satır sonu (`\r\n`) gelir.** [Kesin]
   Bazı programlar bu satır sonunu "Enter" gibi algılar veya alana fazladan boşluk koyar.
   → **Karar:** "Yapıştır" adımında **"Temiz metin olarak yapıştır"** seçeneği vardır (varsayılan açık): panodaki metnin başındaki/sonundaki boşluk ve satır sonları temizlenir.

4. **Ctrl+C anında gerçekleşmez.** [Kesin] Excel panoya geç yazabilir. Sabit bekleme süresi bazen yetmez, bazen gereksiz uzundur.
   → **Karar:** Kopyalama adımından sonra motor panonun **gerçekten değiştiğini** (`AddClipboardFormatListener`) en fazla 2 sn bekler; değişmezse adım hata verir.

5. **Bu makro çalışırken çalışan bilgisayarı kullanamaz.** [Kesin]
   Windows'ta tek bir imleç ve tek bir klavye odağı vardır. Kaydet/oynat makrosu bu ikisini kullanır. Makro çalışırken kullanıcı fareyi oynatırsa makro **duraklar** (varsayılan); devam için bar üzerinden "Devam" denir.

6. **Yönetici olarak çalışan bir programa normal yetkili program tuş/tıklama gönderemez (UIPI).** [Kesin]
   Hedef program yönetici olarak çalışıyorsa makro oraya yazamaz. Uygulama bunu algılar ve açıkça söyler; kendini otomatik olarak yöneticiye yükseltmez.

7. **Döngünün bir bitiş koşulu olmalı.** [Muhtemel]
   Excel listesi bittiğinde makro durmazsa boş hücreleri sonsuza dek hedef programa yapıştırır.
   → **Karar:** Döngü seçenekleri: N kez, sonsuz, **"Kopyalanan değer boşsa dur"** (önerilen, varsayılan). Sonsuz döngü seçilince uyarı gösterilir.

---

## 1. Rol (yapay zekâ için)

```text
<role>
You are a senior Windows desktop engineer specialized in C#, .NET 10, WPF, Win32 interop
(SendInput, low-level hooks, window management, clipboard), and MSI packaging with WiX.
You write small, readable, fully compilable code. You never leave TODOs, stubs, or
"the rest is similar" in delivered code. If a requirement is technically impossible or risky,
you say so first and propose the closest feasible alternative.
All identifiers, comments, commit messages, and log messages are in English.
</role>
```

---

## 2. Ürün tanımı

**Macro Studio**, ofis çalışanının programlar arasında elle yaptığı tekrarlayan işi **bir kez kaydedip** istediği kadar tekrar oynatan, Windows 10/11 için ücretsiz ve açık kaynaklı bir **makro kaydedici/oynatıcıdır.**

Hedef ortam: kurumsal ofisler (Excel ve masaüstü iş uygulamaları). Kullanıcı programcı değildir.

### Örnek senaryo (uygulamanın kesinlikle sorunsuz çalıştırması gereken iş)

1. Excel'de seçili hücreyi kopyala (`Ctrl+C`).
2. Hedef programa geç.
3. İlgili alana tıkla, yapıştır (`Ctrl+V`), `Enter`'a bas.
4. Hedef programda başka bir yeri kopyala.
5. Excel'e dön.
6. İlgili hücreye yapıştır.
7. Bir alt satıra geç.
8. Excel'deki değer boşalana kadar 1–7'yi tekrarla.

Kullanıcı bunu **Kaydet** ile bir kez yapar, **Durdur** der, döngü sayısını ve hızı seçer, **Oynat**'a basar.

---

## 3. Teknoloji kararları

| Konu | Karar | Gerekçe |
|---|---|---|
| Dil | C# (latest) | İstek |
| Çalışma ortamı | **.NET 10 LTS** | [Kesin] .NET 8 desteği Kasım 2026'da bitiyor; .NET 10 Kasım 2028'e kadar destekli. |
| Arayüz | **WPF** | [Muhtemel] Şeffaf, şekilli, her zaman üstte bir pencere (çentik bar) WPF'te en basit; Win10/11'de aynı davranır. |
| Tema | **WPF-UI** (lepoco/wpfui, MIT) | [Muhtemel] Win11 Fluent görünümü, Mica, Win10'da düzgün geri dönüş. Elle yazılacak yüzlerce satır stil kodunu kaldırır. |
| MVVM | **CommunityToolkit.Mvvm** | Kaynak üreteci ile az kod. |
| DI | **Microsoft.Extensions.Hosting** | Tek yerde servis kaydı. |
| Girdi kaydı | `SetWindowsHookEx` (`WH_MOUSE_LL`, `WH_KEYBOARD_LL`) | Kayıt için başka yol yok. **Kanca yalnızca kayıt sırasında açıktır**, kayıt bitince kaldırılır. |
| Girdi gönderme | `SendInput` | Standart, en uyumlu yöntem. |
| Pencere bulma | Win32 (`EnumWindows`, süreç adı + başlık + sınıf) | |
| Loglama | **Serilog** (dosya, günlük döngülü, 14 gün) | |
| Test | xUnit | |
| Kurulum | **WiX Toolset v5**, `.msi` | [Muhtemel] v5 MS-RL lisanslı ve ücretsizdir; v6'nın ek lisans koşullarıyla uğraşmaya gerek yok. **MSI yalnızca proje sahibi "program bitti" dediğinde üretilir.** |
| Yayın | Self-contained, `win-x64`, ReadyToRun | Hedef makinede .NET kurulu olması gerekmez. |
| Lisans | **MIT** | [Muhtemel] Telif proje sahibinde kalır, ücretsiz dağıtıma uygundur. |

**Yasak:** `BinaryFormatter`, kayıt dışında açık kalan kanca, tuş vuruşlarının log dosyasına yazılması, yönetici yetkisi isteyen kod, ağ bağlantısı, telemetri, Outlook/Word/PDF gibi kapsam dışı entegrasyonlar.

---

## 4. Çözüm yapısı

```text
Macro Studio/
├─ MacroStudio.slnx
├─ Directory.Build.props          # Nullable, TreatWarningsAsErrors, LangVersion, Version
├─ Directory.Packages.props       # Central package versions
├─ .editorconfig
├─ LICENSE                        # MIT, Copyright (c) Ahmet Şahin
├─ README.md                      # TR + EN
├─ src/
│  ├─ MacroStudio.Core/           # Models, step types, timing, validation. No WPF, no Win32.
│  ├─ MacroStudio.Engine/         # Recorder, Player, loop and speed logic (talks to Native via interfaces)
│  ├─ MacroStudio.Native/         # All P/Invoke: hooks, SendInput, windows, clipboard, DPI
│  └─ MacroStudio.App/            # WPF: NotchBar, MainWindow, views, view models, themes, resources
├─ tests/
│  ├─ MacroStudio.Core.Tests/
│  └─ MacroStudio.Engine.Tests/
├─ installer/                     # Created only in the final stage
└─ docs/
   └─ DECISIONS.md                # Every assumption the AI made, with reason
```

**Bağımlılık yönü:** `App → Engine → Core`, `App → Native`, `Engine` Native'e yalnızca Core'daki arayüzler üzerinden erişir (test edilebilirlik). Ters yön yasak.

---

## 5. Arayüz 1 — Çentik Bar (Notch Bar)

### 5.1 Görünüm

Ekranın üst kenarının ortasına yapışık, **ters yamuk** (üstü geniş, altı dar, kenarları eğik) şekilli bar. Üst kenarı ekranın üst kenarıyla birleşik, alt köşeleri hafif yuvarlatılmış.

```text
 ┌─────────────────────────── ekran üst kenarı ───────────────────────────┐
 │       \  [⌂]  │ [● Kaydet] [▶ Oynat] [■ Durdur] │ Makro ▾ │ 🔁 10 │ 1.0x  /   │
 │        \__________________________________________________________/     │
```

| Durum | Boyut (%100 ölçekte) | İçerik |
|---|---|---|
| **Kapalı** | 160 × 6 px | İnce şerit. Kayıtta kırmızı, oynatmada vurgu renginde nabız, duraklatılmışta sarı, hatada kırmızı sabit. |
| **Açık** | 640 × 48 px | Ev butonu · Kaydet · Oynat/Duraklat · Durdur · Makro seçici · Döngü sayısı · Hız · Sabitle (📌). Durum metni yalnızca söyleyecek bir şey varken görünür: duraklatıldı ("Duraklatıldı · Döngü 4/10 · Adım 6/9"), son çalışmanın sonucu, hata. Boştayken "Hazır" gibi bilgi vermeyen bir metin gösterilmez. |

Şekil `Path` geometrisiyle çizilir; tüm boyutlar DPI ile ölçeklenir.

### 5.2 Davranış

- **Açılma:** Fare şeridin üzerinde (yatayda şerit ± 40 px, dikeyde ekranın en üst 4 px'i) **250 ms** kalırsa açılır. [Tahmin] Değer ayarlardan 0–1000 ms arası değiştirilebilir.
- **Kapanma:** Fare bardan çıktıktan **600 ms** sonra. Animasyon 150 ms; Windows animasyonları kapalıysa animasyon yok.
- **Sabitle (📌):** Basılınca bar fare çekilse de açık kalır; tekrar basınca normal davranışa döner. Durum ayarlarda saklanır, uygulama yeniden açılınca da sabitli kalır. Sabitliyken ikon dolu ve vurgu renginde. Sabitli bar altındaki pencerelerin üst-orta kısmını (tarayıcı sekmeleri, Excel başlığı) kapatır; kullanıcı Konum ayarından sola/sağa alabilir.
- **Kayıt sırasında** bar kapalı başlar (sabitli olsa bile; açık bar altındaki tıklamaları üstüne alırdı). Fare üzerinde durursa açılır, böylece Durdur butonuna ulaşılır; bara yapılan tıklamalar kayda girmez. Kayıt sırasında kapalı şeritte kırmızı, yanıp sönen bir çizgi görünür.
- **Oynatma sırasında bar kapalı kalır** ve fare üzerine gelse bile açılmaz; oynatmanın kendi fare hareketleri onu açardı. Durdurma bu sırada kısayol tuşuyla yapılır (Bölüm 7.4). Bitince sabitli bar yeniden açılır.
- **Kapalı bar fareye tamamen geçirgendir** (`WS_EX_TRANSPARENT`). Yoksa Windows'un "bu noktadaki pencere hangisi" sorusu (`WindowFromPoint`) barın görünmez kısmını cevap verir ve ekranın üst ortasına yapılan tıklamalar kayıtta kaybolur.
- **Odak çalmaz:** `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`. Bara tıklamak kullanıcının çalıştığı pencereden odağı almaz. Görev çubuğunda ve Alt+Tab'da görünmez.
- **Barın kendisine yapılan tıklamalar kayda girmez.**
- **Her zaman üstte**, fakat:
  - Tam ekran uygulama (sunum, video) varken gizlenir (`SHQueryUserNotificationState`).
  - Kullanıcı pencere sürüklerken gizlenir (`EVENT_SYSTEM_MOVESIZESTART/END`). [Kesin] Win11, sürüklenen pencereyi üst-ortaya getirince "Snap Layouts" çubuğunu tam bu konumda gösterir.
- **Monitör:** Varsayılan birincil; ayarlardan seçilebilir. Monitör çıkarılınca birincile döner. `WM_DISPLAYCHANGE` / `WM_DPICHANGED` ile yeniden konumlanır.
- **Ev butonu (⌂):** Ana pencereyi açar ya da öne getirir. İkon `U+E80F`, font zinciri `Segoe Fluent Icons, Segoe MDL2 Assets`. [Kesin] Win10'da Segoe Fluent Icons yoktur, MDL2'de aynı kod noktası vardır.
- **Sağ tık menüsü:** Ana menüyü aç · Barı 1 saat gizle · Ayarlar · Çıkış.
- Tüm butonlarda araç ipucu ve `AutomationProperties.Name`.

### 5.3 Kabul ölçütleri
- [ ] Win10 ve Win11'de, %100 / %150 / %200 ölçekte şekil bozulmadan çizilir.
- [ ] Farklı ölçekli iki monitörde doğru konumlanır.
- [ ] Not Defteri'nde yazarken bara tıklanınca imleç Not Defteri'nde kalır.
- [ ] Tam ekran PowerPoint sunumunda bar görünmez.
- [ ] Boşta CPU < %0.5, bellek < 120 MB (Görev Yöneticisi'nin "Bellek" sütunu, yani özel çalışma kümesi; ölçümler `docs/DECISIONS.md` Aşama 6'da).

---

## 6. Arayüz 2 — Ana Pencere

Ev butonuyla açılır. Kapatınca uygulama kapanmaz, bar çalışmaya devam eder. Tamamen çıkış: bar sağ tık → Çıkış veya sistem tepsisi simgesi.

Solda gezinme menüsü (`NavigationView`), sağda içerik:

| Sayfa | İçerik |
|---|---|
| **Ana Sayfa** | Son kullanılan makrolar (son çalışmalarının sonucuyla), büyük "Yeni kayıt" butonu, "Tümünü gör" ile Makrolarım. |
| **Makrolarım** | Liste/kart: ad, açıklama, adım sayısı, tur başına tahmini süre, son değişiklik, son çalışma. Arama (ad ve açıklamada), oynat (kayıtlı tekrar ve hızla), düzenle, çoğalt, yeniden adlandır, dışa/içe aktar (`.msmacro`), sil (30 gün çöp kutusu, geri yükle). Okunamayan dosyalar uyarıyla listelenir, silinmez. |
| **Düzenleyici** | Adım tablosu (Bölüm 7.3). |
| **Ayarlar** | Bölüm 9. |
| **Hakkında** | Sürüm, lisans, üçüncü taraf kütüphaneler, log klasörünü aç, **"Made by Ahmet Şahin"** ve GitHub profil bağlantısı. |

**İmza:** Ana pencerenin sol menüsünün en altında küçük, gri, sade **"Made by Ahmet Şahin"** yazısı. Tıklanınca varsayılan tarayıcıda proje sahibinin GitHub profili açılır (`Process.Start` + `UseShellExecute = true`). Bağlantı adresi tek bir sabitte tutulur: `src/MacroStudio.App/Shell/Author.cs` → `GitHubProfileUrl` (`https://github.com/alpha32k`). Aynı imza Hakkında sayfasında da var.

---

## 7. Makro motoru

### 7.1 Adım türleri (Core)

```csharp
public abstract record MacroStep(Guid Id, int DelayBeforeMs, bool IsEnabled, string? Note);

public sealed record MouseClickStep(...)     // Button, ClickCount, Position (window-relative)
public sealed record MouseDragStep(...)      // Start, End, simplified path
public sealed record MouseWheelStep(...)     // Direction, Notches  (flagged as unreliable, see 0.1)
public sealed record KeyPressStep(...)       // VirtualKey, ScanCode, IsExtended
public sealed record HotkeyStep(...)         // Modifiers + Key, e.g. Ctrl+C
public sealed record TypeTextStep(...)       // Unicode text, independent of keyboard layout
public sealed record FocusWindowStep(...)    // ProcessName, TitlePattern, ClassName (see 0.2)
public sealed record CopyStep(...)           // Ctrl+C + wait until clipboard changes (see 0.4)
public sealed record PasteStep(...)          // Ctrl+V, CleanText option (see 0.3)
public sealed record WaitStep(...)           // Fixed wait
```

Makro dosyası:

```csharp
public sealed record MacroDefinition(
    Guid Id, int SchemaVersion, string Name, string Description,
    ScreenInfo RecordedOn,               // resolution + DPI, to warn on mismatch
    LoopSettings Loop,                   // Count | Infinite | UntilClipboardEmpty
    double SpeedMultiplier,              // 0.25 - 5.0
    IReadOnlyList<MacroStep> Steps);
```

### 7.2 Kaydet (Record)

- Bar veya kısayol (`Ctrl+Alt+R`) ile başlar. **3 saniye geri sayım** (ayarlanabilir, 0 = yok); bu sürede kullanıcı başlangıç penceresine geçer.
- Yakalananlar: sol/sağ/orta tık, çift tık, sürükleme, tekerlek, tuşlar ve kombinasyonlar, her adımdan önceki bekleme süresi (`Stopwatch`, ms).
- **Akıllı dönüştürmeler** (her biri ayarlardan kapatılabilir):
  - `Ctrl+C` → `CopyStep`
  - `Ctrl+V` → `PasteStep`
  - `Alt+Tab` / görev çubuğundan pencere değişimi → `FocusWindowStep`
  - Excel penceresinde tekerlek → kayıt bitince "↓ tuşuna çevir" önerisi
  - Art arda basılan harf tuşları → tek `TypeTextStep`
- Tıklama konumu **pencereye göre** saklanır (pencere taşınsa da doğru yere tıklar). Ekranın kendisine (masaüstü, görev çubuğu) yapılan tıklama ekran konumuyla saklanır.
- Uygulamanın kendi kısayolları ve bara yapılan tıklamalar kayda girmez.
- **Parola alanlarına yazılanlar kaydedilmez** (UI Automation `IsPassword`); yerine "parola alanı – elle girin" notlu bekleme adımı konur.
- Kanca geri çağrısı yalnızca olayı bir kuyruğa (`Channel<T>`) atıp döner (< 1 ms). Aksi hâlde [Kesin] Windows yavaş kancayı sessizce devre dışı bırakır.
- Kayıt `Ctrl+Alt+R` tekrar veya bar → Durdur ile biter; kayıt sonunda makroya ad verme penceresi açılır.

### 7.3 Düzenleyici

Adım tablosu sütunları: # · Etkin ☑ · Tür (ikonlu) · Açıklama (ör. "Ctrl+C – Excel", "Tıkla – SAP Easy Access") · Bekleme (ms) · Not.

- Sil, çoğalt, sürükle-bırak ile sırala, devre dışı bırak, bu adımdan itibaren oynat, tek adımı test et. Oynatma komutları **kaydedilmemiş** hâli oynatır; değişiklik kaydetmeden denenebilir.
- Araya adım ekle (menüden: tuş, tık, metin yaz, bekle, pencereyi öne getir, kopyala, yapıştır). Yeni adım seçimin arkasına girer ve önündeki adımın penceresini hedef alır; "tık" geri sayımla imlecin yerini alır.
- Bekleme hücresi şunları kabul eder: `230`, `230 ms`, `0,23 sn`, `0.23 s`.
- Toplu işlem: seçili adımların beklemesini ayarla (ör. hepsini 150 ms yap).
- Geri al / yinele (en az 100 adım; yalnızca adımlar, ad/tekrar/hız hariç).
- Uyarı ikonları: Excel'de tekerlek adımı, Alt+Tab'dan dönüşmemiş tuş. Kayıt çözünürlüğü/ölçeği farklıysa sayfanın üstünde uyarı şeridi.
- Makronun adı, tekrarı ve hızı da burada değişir. Kayıt önceki sürümü saklar; "Önceki sürüme dön" onu geri getirir.
- Kaydedilmemiş değişiklik varken başka makro seçmek veya uygulamadan çıkmak kaydetmeyi sorar.

### 7.4 Oynat (Play), döngü, hız

- **Başlatma:** Bar → Oynat veya `Ctrl+Alt+P`. İsteğe bağlı geri sayım.
- **Döngü:** N kez (1–100.000) · Sonsuz (uyarılı) · **Kopyalanan değer boşsa dur** (varsayılan). "Boşsa dur"da karar her turun **ilk** kopyalamasına bakar (listenin sıradaki hücresi); sonraki kopyalamalar (hedef programdan geri kopyalanan değer) boş olsa da tur sürer. Makroda hiç kopyalama yoksa bir kez oynatılır.
- **Hız:** 0.25x / 0.5x / 1x / 1.5x / 2x / 3x / 5x — tüm beklemeler bu oranla bölünür. Ayrıca "sabit bekleme" modu: tüm adımlar arası X ms.
- **Duraklat / Devam:** `Ctrl+Alt+P` (oynatma sırasında) veya bar. Duraklama yalnızca **adımlar arasında** devreye girer, bir adımın ortasında asla: duraklatılmış bir makro Ctrl'yi basılı bırakırsa kullanıcının yazdığı her harf kısayola dönüşür.
- **Acil durdurma:** **`Esc`** (oynatma sırasında global olarak, `RegisterHotKey` değil kanca ile, yalnızca oynatma boyunca; programa ulaşmaz, böylece oradaki bir pencereyi kapatmaz) ve `Ctrl+Alt+End` (yalnızca oynatma boyunca kayıtlı; [Kesin] Uzak Masaüstü'nde Ctrl+Alt+Del yerine geçer). **50 ms içinde** durur ve basılı kalmış tüm tuşları/fare butonlarını bırakır (aksi hâlde [Kesin] Ctrl veya Shift "takılı" kalır).
- **Kullanıcı müdahalesi:** Oynatma sırasında gerçek fare hareketi/tuş algılanırsa (enjekte edilmemiş girdi, `LLMHF_INJECTED` bayrağıyla ayrılır) → duraklat (varsayılan) / durdur / yok say. Macro Studio'nun kendi kısayolları ve tek başına değiştirici tuşlar müdahale sayılmaz. Devam'dan sonraki 1 saniye de sayılmaz (el fareden çekilirken).
- **Bar oynatma sırasında:** Tekrar ve hız seçicilerinin yerinde ilerleme görünür ("Tur 4/10 · Adım 6/9"; duraklatılınca başında "Duraklatıldı"). Kapalı şerit oynarken vurgu renginde yanıp söner, duraklatılınca sarı yanar.
- **Temiz yapıştırma** panoya düz metin yazar; Excel'in zengin biçimi (renk, formül) yapıştırılmaz. Biçim gerekiyorsa adımda "Temiz metin" kapatılır.
- **Pencere eşleştirme:** Önce program adı + pencere sınıfı, başlık yalnızca birden çok aday varken ayırt etmek için. [Kesin] Başlıklar değişir: Not Defteri metin değişince başa `*` ekler, Excel dosya adını gösterir. Tam başlık eşleştirmesi bu yüzden kırılır.
- **Hedef pencere bulunamazsa:** 5 sn bekle (ayarlanabilir), sonra dur ve "Şu pencere bulunamadı: <program – başlık>" mesajı göster.
- **Pencere boyutu değiştiyse:** Tıklama, sürükleme ve tekerlek adımlarından önce pencere kayıttakinden farklı boyuttaysa kayıttaki yerine ve boyutuna getirilir (büyütülmüşse önce geri yüklenir). Kayıttaki yer artık bağlı bir monitörde değilse dokunulmaz. Tuş adımlarında pencereye dokunulmaz.
- **Windows girdiyi reddederse** (ekran kilitlendi, UAC penceresi açıldı): `SendInput` daha az olay teslim eder; makro o adımda durur ve nedenini söyler, "çalışıyor gibi" devam etmez.
- **Farklı ekranda kaydedilmiş makro:** Tıklama/sürükleme/tekerlek içeren bir makro farklı çözünürlük veya ölçekte oynatılacaksa oynatmadan önce bir kez sorulur (uygulama açık kaldıkça makro başına bir kez). Yalnızca klavye kullanan makrolar sorulmaz.
- **Zamanlama doğruluğu:** Beklemeler ±10 ms içinde tekrarlanır (`Stopwatch` + kısa aralıklarla `Thread.Sleep(1)` / `timeBeginPeriod(1)` yalnızca oynatma boyunca).
- Oynatma ayrı bir arka plan thread'inde çalışır; UI hiç donmaz.
- Bitişte bildirim: "Tamamlandı: 118 döngü, 4 dk 12 sn" veya hatanın nedeni.

### 7.5 Saklama
- `%LocalAppData%\MacroStudio\` → `macros\*.msmacro` (JSON), `settings.json`, `logs\`.
- `System.Text.Json` kaynak üreteciyle, `SchemaVersion` ve sürüm yükseltme zinciri.
- Atomik kayıt (`.tmp` → `File.Move` ile üzerine yazma), her kayıtta önceki sürüm `macros\.history\<ad>\` altında saklanır (son 10).
- Silinen makro `macros\.trash\<zaman>\` altına taşınır (sürümleriyle birlikte), 30 gün geri yüklenebilir; uygulama açılışta 30 günü geçenleri kalıcı siler.
- İçe aktarılan makro kütüphaneye **eklenmeden önce** adım listesini gösterir ve onay ister (yeni kimlikle eklenir; ad doluysa "Ad (2)" önerilir).
- Her makronun son tam çalışması (bitiş zamanı, sonuç, tur) ayrı bir dosyada (`runs.json`) tutulur; oynatmak makro dosyasını hiç değiştirmez.
- Loglarda tuş içerikleri ve kopyalanan metin **asla** yer almaz; yalnızca adım türü, süre, sonuç.

---

## 8. Dil (Türkçe / İngilizce)

- Tüm görünen metinler `.resx`: `Strings.resx` (İngilizce, nötr) ve `Strings.tr.resx` (Türkçe). Kodda sabit görünen metin **yasak**.
- İlk açılışta Windows dili Türkçe ise Türkçe, değilse İngilizce. Ayarlardan değiştirilir, **yeniden başlatmadan** anında uygulanır.
- Cümle birleştirme yok; tek kaynak metni + `string.Format`.
- **Türkçe "I" tuzağı** [Kesin]: Türkçe kültürde `"file".ToUpper()` → `"FİLE"`. Kod içi karşılaştırmalar `StringComparison.OrdinalIgnoreCase`, dönüşümler `ToUpperInvariant()`, ayrıştırmalar `CultureInfo.InvariantCulture`. Yalnızca kullanıcıya gösterilen sayı/tarih seçili kültürle biçimlenir.
- `TypeTextStep` Türkçe karakterleri (ğ, ü, ş, ı, İ, ö, ç) klavye düzeninden bağımsız yazar (`KEYEVENTF_UNICODE`).
- Log dosyaları, JSON alan adları, hata kodları her zaman İngilizce.

---

## 9. Tema ve Ayarlar

### Tema (Win10/Win11 uyumlu)
- Seçenekler: **Sistem (varsayılan)**, Açık, Koyu.
- Sistem teması `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` değerinden okunur; değişiklik `SystemEvents.UserPreferenceChanged` ile anında yakalanır.
- Vurgu rengi Windows'tan; ayarlardan özel renk seçilebilir.
- **Win11:** Mica arka plan, yuvarlak köşeler, koyu başlık çubuğu, `Segoe UI Variable`.
- **Win10:** Düz arka plan, köşeli pencere, koyu başlık çubuğu (build 19041+), `Segoe UI`.
- **Yüksek Karşıtlık** açıksa sistem renkleri kullanılır.
- Çentik bar aynı temayı kullanır: açıkta beyaza yakın, koyuda koyu gri; yarı opak, 1 px kenar.

### Ayarlar sayfası
- **Genel:** Dil, tema, vurgu rengi, Windows ile başlat (HKCU `Run`).
- **Çentik Bar:** Monitör, konum (sol/orta/sağ), açılma gecikmesi, gizle.
- **Kısayollar:** Kaydet, Oynat/Duraklat, Acil durdur, Ana pencere (`Ctrl+Alt+M`). Çakışma varsa uyarı.
- **Kayıt:** Geri sayım, akıllı dönüştürmeler (tek tek aç/kapa). Fare hareketleri yalnızca sürüklemenin yolu olarak kaydedilir; tıklamalar konumlarıyla.
- **Oynatma:** Varsayılan hız, varsayılan döngü türü, kullanıcı müdahalesinde ne yapılsın, pencere bekleme süresi, temiz metin yapıştırma varsayılanı.
- **Veri:** Makro klasörü, logları aç, ayarları dışa/içe aktar.

---

## 10. Kod yazım kuralları

1. Tüm kod, yorum, log, commit mesajı **İngilizce**.
2. `Nullable` açık, `TreatWarningsAsErrors` açık, .NET analizörleri açık.
3. Bir sınıf tek iş yapar; metot ~40 satırı geçerse bölünür; dosya başına bir tip.
4. **Gereksiz kod yok:** kullanılmayan `using`/sınıf/parametre, "ileride lazım olur" kodu, yoruma alınmış kod bulunmaz. Her NuGet paketi `docs/DECISIONS.md` içinde gerekçelendirilir.
5. Yorum yalnızca "neden"i açıklar.
6. Tüm Win32 çağrıları `MacroStudio.Native` içinde, `LibraryImport` ile.
7. `async/await` + `CancellationToken`; UI thread asla bloklanmaz.
8. Anlamlı, kısaltmasız İngilizce adlar (`MacroRecorder`, `MacroPlayer`, `NotchBarWindow`, `FocusWindowStep`).
9. Zamanlama, döngü, hız, dönüştürme ve JSON için birim testi; Win32 bağımlılıkları arayüz arkasında, testte sahte (fake) sürümle.

---

## 11. Çalışma yöntemi (aşamalar)

Her aşamanın sonunda: derlenen eksiksiz kod, geçen testler, değişen dosya listesi, nasıl derlenip çalıştırılacağı, varsayımlar (`docs/DECISIONS.md`), açık sorular. **Proje sahibinin onayı olmadan sonraki aşamaya geçilmez.**

| Aşama | Kapsam | Bitti sayılması için |
|---|---|---|
| **0** | Anladıklarını en fazla 10 maddede özetle, en fazla 5 soru sor. Kod yok. | Onay |
| **1 — GUI** | Çözüm iskeleti, Çentik Bar (tam davranışıyla, butonlar henüz işlevsiz), Ana Pencere ve tüm sayfaların boş iskeleti, tema (Sistem/Açık/Koyu), dil (TR/EN), Ayarlar sayfası (kaydedip geri yükleme dahil), sistem tepsisi. | Bölüm 5.3 kabul ölçütleri. |
| **2 — Kaydet** | Kancalar, olay kuyruğu, adım üretimi, akıllı dönüştürmeler, pencereye göre konum, makro kaydetme/açma (JSON). | Örnek senaryo (Bölüm 2) kaydedilip dosyada doğru adımlar görülüyor. |
| **3 — Oynat** | Oynatıcı, döngü (3 tür), hız, duraklat/devam, acil durdurma, kullanıcı müdahalesi, pano senkronu, pencere bulma. | Örnek senaryo Excel'de 50 satırla 1x ve 2x hızda hatasız çalışıyor; liste bitince kendiliğinden duruyor. |
| **4 — Düzenleyici** | Adım tablosu, ekle/sil/sırala, bekleme düzenleme, geri al/yinele, adımdan itibaren oynat. | Kaydedilmiş makronun beklemeleri düzenlenip tekrar oynatılabiliyor. |
| **5 — Makrolarım ve sağlamlık** | Makrolarım sayfası, içe/dışa aktarma, çözünürlük uyarısı, UIPI algılama, hata mesajları, bildirimler, loglama. | Win10 ve Win11'de, %100 ve %150 ölçekte senaryo çalışıyor. |
| **6 — Cilalama** | Erişilebilirlik, performans ölçümü, README (TR/EN), LICENSE, **"Made by Ahmet Şahin" + GitHub bağlantısı.** | Tüm kabul ölçütleri geçiyor. |
| **7 — Kurulum** | **Yalnızca proje sahibi "program bitti" dediğinde:** WiX v5 ile kullanıcı bazlı (admin gerektirmeyen) `.msi`, TR/EN kurulum ekranları, Başlat menüsü ve isteğe bağlı masaüstü kısayolu, `.msmacro` ilişkilendirmesi, `MajorUpgrade`, kaldırınca kullanıcının makroları silinmez, kod imzalama adımı. Uygulandığı hâli: iki MSI (tr-TR, en-US), `installer/build.ps1` ile; ayrıntılar `docs/DECISIONS.md` Aşama 7. | MSI temiz Win10 ve Win11'e kurulup kaldırılabiliyor. |

---

## 12. Belirsizlik kuralı

Bir gereksinim belirsizse yapay zekâ **daha güvenli ve daha basit** seçeneği seçer, `docs/DECISIONS.md`'ye yazar ve aşama raporunda söyler. Bir istek teknik olarak imkânsızsa önce bunu söyler, sonra en yakın uygulanabilir alternatifi önerir.

---

## 13. Açık sorular (proje sahibine)

1. Hedef program hangisi? (SAP GUI, bir web sayfası, şirket içi bir program…) Hedef program bilinirse pencere bulma ve bekleme ayarları ona göre test edilir.
2. Hedef program "Enter" sonrası bir sonraki ekranı yüklerken ne kadar bekliyor? Değişken mi? (Değişkense ileride "ekranda şu görünene kadar bekle" adımı gerekir.)
3. GitHub profil adresi (Aşama 6'da eklenecek).
