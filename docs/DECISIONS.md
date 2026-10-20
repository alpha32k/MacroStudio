# Kararlar ve Varsayımlar

Prompt'ta açıkça yazmayan veya prompt'tan bilerek sapılan her karar burada, nedeniyle birlikte durur.
Etiketler: **[Kesin]** doğrulandı · **[Muhtemel]** güçlü çıkarım · **[Tahmin]** değiştirilebilir seçim.

---

## 1.0.1 — Windows panelleri ve gizli pencereler (2026-10-20)

Kullanıcı hatası: Windows tuşu/arama kutusu → metin → Enter (tarayıcı açılır) kaydı, oynatmada 2. adımda "pencere bulunamadı" diye durdu.

### Nedenler

1. **Windows'un kendi panelleri program penceresi sanılıyordu.** Arama paneli (`SearchApp`, sınıf `Windows.UI.Core.CoreWindow`) kayda hedef pencere olarak giriyordu. Oynatmada Windows tuşu önce Başlat'ı açıyor, arama paneli ancak yazınca beliriyor. Makro yazmadan önce paneli beklediği için onu hiç bulamıyordu.
2. **Gizli kabuk pencereleri "öne getir" olarak kaydediliyordu.** Arama kapanırken Windows odağı bir anlığına görünmez bir pencereye (`explorer` / `ApplicationManager_DesktopShellWindow`) veriyor. Kaydedici öne gelen her pencereyi kaydettiği için bu da adım oluyordu; oynatmada görünmediği için bulunamazdı.

### Çözüm

- Yeni pencere türleri: `ShellPanel` (Başlat, arama, bildirim ve hızlı ayarlar paneli) ve `Hidden` (görünmeyen pencere). İkisi de görev çubuğu gibi ele alınıyor: adımlar pencere hedefi olmadan kaydediliyor, "öne getir" adımı yazılmıyor.
- Oynatıcı eski kayıtlardaki bu hedefleri (`ShellPanels`) beklemeden geçiyor; tuşlar o an açık olan panele gidiyor. Var olan makrolar yeniden kaydedilmeden düzeliyor.

### Testler

146 testin 146'sı geçiyor; dört yeni test eklendi (arama paneline yazma, gizli pencereye odak kaydı, eski kayıtta arama adımları, eski kayıtta gizli pencere). Gerçek Windows araması üzerinde oynatma denenmedi.

---

## GitHub hazırlığı (2026-10-20)

- İmza bağlantısı `https://github.com/alpha32k` oldu (uygulama, Hakkında, README, Ayarlar > Uygulamalar'daki bağlantı). Bağlantı boşken imzayı düz yazıya çeviren kod artık gereksiz olduğu için kaldırıldı.
- Kurulum dosyaları yeniden üretildi; sürüm 1.0.0 kaldı, çünkü önceki 1.0.0 hiç dağıtılmadı.
- Tasarım belgesi kök klasörden `docs/` altına taşındı.
- Tasarım belgesindeki "Hedef ortam: Arçelik / Beko ofisleri" satırı "kurumsal ofisler" yapıldı. [Tahmin] Bir şirket adının halka açık bir depoda geçmesi proje sahibi için sorun olabilir; teknik bir bilgi de taşımıyor.
- `.gitattributes` eklendi: metin dosyalarının satır sonları depoda tek tip; simge ve görseller ikili dosya.
- Depoya girmeyenler (`.gitignore`):
  - Derleme çıktıları (`bin/`, `obj/`) ve test sonuçları.
  - Visual Studio'nun kullanıcıya özel klasörü (`.vs/`; içinde kullanıcının Windows yolu var).
  - Üretilen kurulum dosyaları (`installer/out/`). Bunlar GitHub'da "Releases" sayfasına ayrıca yüklenir.

---

## Aşama 7 — Kurulum (2026-10-20)

Proje sahibi "7. aşamaya geç" dedi; bu, "program bitti" onayı sayıldı.

### Yapı

- **`installer/` dosyaları:**
  - `MacroStudio.Installer.wixproj` (WiX v5.0.2 SDK).
  - `Package.wxs`.
  - `Strings.tr-TR.wxl`, `Strings.en-US.wxl`.
  - `License.rtf`.
  - `Dialog.bmp`, `Banner.bmp` (uygulama simgesinden `make-images.ps1` ile çizildi).
  - `build.ps1`: yayınla → (sertifika varsa) imzala → MSI → (sertifika varsa) imzala → `installer/out`.
- **Uygulama:**
  - Sürüm 1.0.0.
  - `.msmacro` dosyası argüman olarak gelince açılıyor.
  - İkinci kopya isteği çalışan kopyaya artık adlandırılmış bir kanalla (pipe, yalnızca aynı kullanıcı açabilir) iletiyor; eskiden yalnızca "pencereni göster" sinyali vardı.

### Kararlar

1. **Kullanıcı bazlı kurulum, `%LocalAppData%\Programs\Macro Studio`.** WiX'in `Scope="perUser"` seçeneği tek başına Program Files'ı hedefler ve yönetici ister; bu yüzden klasör açıkça yerel uygulama verisi altında. Kullanıcı bazlı kurulumun per-machine kurallarına takılan ICE38/64/91 denetimleri kapatıldı. ICE69 da kapatıldı: kısayolların başka bileşendeki exe'yi göstermesi güvenli, çünkü masaüstü kısayolu özelliği ana özelliğin altında.

2. **İki ayrı MSI** (tr-TR ve en-US), çünkü tek MSI içinde dil dönüşümü (transform) ek araç ve adım ister. Kod sayfası ikisinde de 1254: "Ahmet Şahin"deki Ş, İngilizce sayfada (1252) yazılamıyor.

3. **Masaüstü kısayolu, kurulumdaki özellik listesinden çıkarılabilen bir özellik** (WixUI_FeatureTree). Ayrı bir onay kutusu için WiX'in pencerelerini değiştirmek gerekirdi.

4. **`.msmacro` ilişkilendirmesi doğrudan `HKCU\Software\Classes` altına yazılıyor.** WiX'in ProgId öğesi HKEY_CLASSES_ROOT'a yazar, doğrulama da onu makine geneli sayıp hata veriyor. Çift tıklanan dosya:
   - kütüphanedeyse düzenleyicide açılıyor,
   - değilse adımları gösterilerek içe aktarılıyor.

5. **"Windows ile başlat" kaldırmada siliniyor.** Bu kaydı program kendisi yazıyor, MSI'ın sahip olduğu bir kayıt değil. Kaldırma sırasında küçük bir işlem (WixQuietExec + `reg delete`) siliyor; yükseltmede dokunmuyor.

6. **Kurulum bitince "Macro Studio'yu başlat" seçili geliyor.**

7. **Kod imzalama isteğe bağlı bir adım:** `build.ps1 -CertificateThumbprint`. Bu makinede sertifika ve signtool (Windows SDK) yok; üretilen MSI'lar **imzasız**, SmartScreen "tanınmayan yayımcı" diyecek.

8. **Yükseltme:** sabit UpgradeCode `07A54CBD-B8CF-457E-943B-3CFD8867C3C8` (asla değişmemeli) + MajorUpgrade. Her sürümde `Version` artırılmalı. `build.ps1` WiX'i her seferinde sıfırdan derliyor: test sırasında, yalnızca sürüm değişince artımlı derlemenin eski MSI'ı koruduğu görüldü.

### Testler (bu bilgisayarda, Win10, kullanıcı bazlı, sessiz kurulum)

- **tr-TR ve en-US MSI:** ikisi de kuruldu (çıkış kodu 0).
  - 283 dosya, Başlat menüsü ve masaüstü kısayolu, `.msmacro` ilişkilendirmesi oluştu.
  - Kurulan program açıldı. Bir `.msmacro` dosyası kabuk üzerinden açılınca çalışan kopyada içe aktarma penceresi göründü.
- **Kaldırma (çıkış kodu 0):**
  - Klasör, kısayollar, ilişkilendirme, kayıt anahtarları, ürün kaydı ve "Windows ile başlat" değeri silindi.
  - Kullanıcının makroları (`%LocalAppData%\MacroStudio`) yerinde kaldı.
- **Yükseltme:** 1.0.0 → 1.0.1 eskisini değiştirdi, kurulu tek sürüm 1.0.1 kaldı. Eski sürümü yenisinin üstüne kurmak reddedildi.
- **Kurulum ekranı:** Türkçe ("Kurulum Sihirbazı'na Hoş Geldiniz") ve yeni görsellerle görüldü. Ekranlardan geçerek kurulum (ileri, lisans, özellik listesi) elle yapılmadı; sessiz kurulumla test edildi.
- **Bitti ölçütü "temiz Win10 ve Win11'e kurulup kaldırılabiliyor": karşılanmadı.**
  - Bu bilgisayar temiz değil (geliştirme makinesi).
  - Win11 yok.

---

## Aşama 6 — Cilalama (2026-10-20)

### Yapılanlar

- **İmza:** "Made by Ahmet Şahin" ana pencerenin sol menüsünün en altında (küçük, gri) ve Hakkında sayfasında.
  - Bağlantı tek sabitte: `Shell/Author.cs` → `GitHubProfileUrl`.
  - Proje sahibi adresi daha göndermedi. Sabit boşken imza tıklanamayan düz yazı; adres yazılınca tıklanınca tarayıcıda açılır.
- **Erişilebilirlik:** Her buton, kutu, onay kutusu ve kaydırıcının artık ekran okuyucunun okuyacağı bir adı var. Daha önce adsız olanlar:
  - Bardaki simge butonları ve seçiciler.
  - Ayarlar'daki 15 kontrol.
  - Düzenleyicinin kutuları.
  - Kaydet penceresi ve soru penceresi.
  - WPF UI'nin başlık çubuğu butonları ve menü butonu: kütüphane bunlara ad vermiyor, `TemplateButtonNames` dil değişince de yeniden adlandırıyor.
- **Performans:** arka plan çöp toplayıcısı kapatıldı (aşağıda).
- **README.md** (TR + EN) ve **LICENSE** (MIT, Copyright (c) 2026 Ahmet Şahin).

### Kararlar

1. **Arka plan çöp toplama kapalı** (`ConcurrentGarbageCollection=false`). Açıkken ikinci bir toplayıcı için ayrılan bellek, uygulamanın ayırdığı belleği iki katından fazla büyütüyordu: 144 MB'a karşı 68 MB [Kesin, ölçüldü]. Bedeli: tam toplama ön planda çalışır. Yığın küçük olduğu için bu birkaç milisaniyelik bir duraklama demek.

2. **Bellek ölçütünün anlamı:** Prompt "bellek < 120 MB" diyor ama hangi sayı olduğunu söylemiyor.
   - Görev Yöneticisi'nin "Bellek" sütunu (özel çalışma kümesi) ölçütü her durumda karşılıyor.
   - Toplam çalışma kümesi (Windows ve WPF DLL'lerinin paylaşılan sayfaları dahil) yalnızca bar varken 125 MB, ana pencere bir kez açıldıktan sonra 160 MB. Bu sayıyla ölçüt karşılanmıyor.
   - Ana pencere kapatılınca yok edilmiyor, gizleniyor; belleği bu yüzden geri dönmüyor. Pencereyi her kapanışta yok edip yeniden kurmak açılışı yavaşlatırdı. Çalışma kümesini zorla boşaltmak da yalnızca sayıyı güzelleştirirdi. İkisi de yapılmadı.

3. **README'de kurulum bölümü yok:** MSI 7. aşamada, proje sahibi "program bitti" deyince yapılacak. README şimdilik kaynaktan derlemeyi anlatıyor.

### Ölçümler (Release, self-contained, ReadyToRun; Win10, 1920×1080, %100)

| Durum | Görev Yöneticisi belleği | Toplam çalışma kümesi | Ayrılan bellek | Boşta CPU |
|---|---|---|---|---|
| Yalnızca bar | 40 MB | 125 MB | 68 MB | %0,017 |
| Ana pencere açık | 64 MB | 159 MB | 141 MB | %0,070 |
| Ana pencere kapatıldıktan sonra | 64 MB | 160 MB | 141 MB | %0,008 |

Hedefler: CPU < %0,5 karşılanıyor. Bellek, yukarıdaki 2. karara bağlı. Ölçümün öncesi (arka plan toplayıcı açık, yalnızca bar): 65 MB / 166 MB / 144 MB.

### Kabul ölçütleri (prompt 5.3) — durum

- [ ] Win10 ve Win11'de, %100 / %150 / %200 ölçekte şekil bozulmadan çizilir: **yalnızca Win10 %100 görüldü.** Ölçeği değiştirmek sistem ayarı; proje sahibinin denemesi gerekiyor.
- [ ] Farklı ölçekli iki monitörde doğru konumlanır: iki monitör aynı ölçekte (%100). Bar 2. monitörde doğru ortalandı. **Farklı ölçekte denenmedi.**
- [ ] Not Defteri'nde yazarken bara tıklanınca imleç Not Defteri'nde kalır: fare gerektiriyor, **bu aşamada denenmedi.**
- [ ] Tam ekran PowerPoint sunumunda bar görünmez: **denenmedi.**
- [x] Boşta CPU < %0,5: %0,02.
- [~] Bellek < 120 MB: Görev Yöneticisi'nde 40–64 MB; toplam çalışma kümesi 125–160 MB.

### Testler

- **Birim testleri:** 142 testin 142'si geçiyor.
- **UI Automation taraması:** bar, Ana Sayfa, Makrolarım ve Düzenleyici'de adsız etkileşimli öğe sayısı 4'ten 0'a indi. Ayarlar ve Hakkında taranamadı: o sayfalara giden menü öğeleri UI Automation ile tıklanamıyor (WPF UI kısıtı). Klavyeyle ve ekran okuyucunun kendi gezintisiyle ulaşılabilmeleri gerekir; denenmedi.

---

## Aşama 5 — Makrolarım ve sağlamlık (2026-10-15)

### Yapı

- **Core:**
  - `MacroLibrary`: `Rename`, `MoveToTrash`, `ListTrash`, `Restore`, `EmptyOldTrash` (`TrashEntry`).
  - Çalışma ve ekran: `RunHistoryStore` + `LastRun` (son çalışma), `MacroDuration` (tur süresi tahmini), `ScreenInfo.Matches`, `ScreenCheck`.
  - Oynatma: `PlaybackFailure.InputBlocked` + `InputBlockedException`; `IWindowSystem.TryMove`.
  - Doğrulama: açıklama sınırı (500 karakter).
- **Engine:** `MacroPlayer`, Windows girdiyi reddedince duruyor. Tıklama/sürükleme/tekerlek öncesinde boyutu değişmiş pencereyi kayıttaki yerine getiriyor.
- **Native:**
  - `InputSimulator`, `SendInput`'un teslim ettiği olay sayısını kontrol ediyor.
  - `WindowSystem.TryMove`, `Monitors.DescribePrimary`.
- **App:**
  - Makrolarım sayfası (`MacrosViewModel`).
  - Yeni pencere ve servisler: `SaveMacroWindow` (kayıttan sonra ve içe aktarmada ortak; eski `SaveRecordingWindow`'un yerini aldı), `RunHistoryService`, `MacroSummary`, `ScreenWarningText`.
  - Ana Sayfa: "Yeni kayıt", "Tümünü gör", son çalışma satırı.
  - Düzenleyiciye açıklama alanı eklendi.

### Kararlar

1. **Boyutu değişmiş pencere kayıttaki yerine ve boyutuna getiriliyor** (yalnızca tıklama, sürükleme, tekerlek adımlarında). Konumlar pencerenin sol üst köşesine göre taşınıyordu. Sağa veya alta yapışık bir buton, pencere küçülünce yer değiştirir ve tıklama sessizce yanına düşer; yanlış alana veri girer. Bu yüzden pencere kayıttaki konuma ve boyuta alınıyor. Bedeli şu: oynatma kullanıcının pencere düzenini değiştiriyor. Kayıttaki yer artık bağlı olmayan bir monitördeyse pencereye dokunulmuyor. Tuş adımları (Excel senaryosunun çoğu) pencereye hiç dokunmuyor.

2. **UI Automation ile öğe bulma (Aşama 3, karar 2) yapılmadı.** [Muhtemel] Doğru çözüm o: butonu adından bulmak, ölçek değişikliğine de dayanır. Ama programdan programa çok farklı davranıyor. Excel hücrelerinde yavaş. SAP GUI gibi programlarda UIA desteği sınırlı. Yanlış öğeyi bulursa da yine sessizce yanlış yere tıklar. Kararı proje sahibiyle konuşmak gerekiyor. Şimdilik 1. karar ve ekran uyarısı riski azaltıyor.

3. **Windows girdiyi reddedince makro duruyor.** [Muhtemel] Ekran kilitlenince veya UAC penceresi açılınca `SendInput` olayları teslim etmiyor. Eskiden makro bunu görmeden "kopyaladım, yapıştırdım" diye devam ederdi. Artık o adımda "ekran kilitli veya yönetici penceresi açık" diyerek duruyor (testli). Kilitlenmeyi önleyen bir ayar eklenmedi. [Muhtemel] `SendInput` zaten Windows'un boşta kalma sayacını sıfırlıyor, bu yüzden makro çalışırken ekran kendiliğinden kilitlenmez.

4. **Farklı ekran uyarısı yalnızca tıklayan makrolar için ve makro başına bir kez soruluyor.** Klavye adımları ekran boyutundan etkilenmiyor. Her oynatmada sormak, uyarıyı okunmayan bir tıklamaya çevirirdi. Düzenleyicideki şerit de aynı kuralı kullanıyor.

5. **İçe aktarma onayı ilk çalıştırmada değil, eklerken soruluyor** (prompt 7.5'ten sapma). [Tahmin] Kullanıcı dosyaya güvenip güvenmeyeceğine onu eklerken karar veriyor. Onaylanmamış makro diye ayrı bir durum tutmak kodu büyütürdü. İçe aktarılan makro yeni bir kimlik alıyor: aynı dosyayı iki kez almak iki ayrı makro yapar, son çalışmaları karışmaz.

6. **Silme 30 günlük çöp kutusuna** (`macros\.trash\<zaman>\`): makro dosyası ve kayıtlı sürümleri birlikte taşınıyor. Geri yüklenince sürümler de geri geliyor. 30 günü geçenleri uygulama açılışta kalıcı siliyor. Silmeden önce soruluyor.

7. **Yeniden adlandırma dosyayı da yeniden adlandırıyor**, kayıtlı sürümler de taşınıyor. Düzenleyici açık makroyu artık yoluyla değil kimliğiyle izliyor. Yoksa Makrolarım'dan yeniden adlandırılan bir makroyu düzenleyici eski yola kaydeder ve kopyasını yaratırdı. Açık makro silinirse düzenleyici onu kapatıyor.

8. **Son çalışma ayrı dosyada** (`runs.json`). Makro dosyasına yazılsaydı her oynatma dosyayı değiştirir ve sürüm geçmişini doldururdu. "Buradan oynat" ve "Bu adımı dene" son çalışma sayılmıyor.

9. **Tur süresi bir tahmin** ("~3 sn"): kayıttaki beklemeler, bekleme adımları ve sürüklemeler, kayıtlı hıza bölünerek. Pencere ve pano beklemeleri bilinmediği için gerçek süre biraz daha uzun.

10. **Okunamayan dosyalar** Makrolarım'da isimleriyle uyarı olarak gösteriliyor ve yerinde bırakılıyor; daha önce yalnızca loga yazılıyordu.

11. **UIPI algılama, hata mesajları, bildirimler, loglama** Aşama 3'te yapılmıştı. Bu aşamaya yalnızca yeni hata ("girdi reddedildi") ve yaşlanan çöp kutusu kaydı eklendi.

### Testler

- **Birim testleri:** 142 testin 142'si geçiyor. Yeni testler:
  - Kütüphane: yeniden adlandırma (sürümlerle), kendi adına yeniden adlandırma, çöp kutusu + geri yükleme (sürümlerle), 30 gün temizliği.
  - Son çalışma kaydı (bozuk dosya dahil), ekran eşleşmesi, ekran uyarısı kuralı, tur süresi.
  - Oynatıcı: boyutu değişmiş pencerenin geri getirilmesi, tuş adımında dokunulmaması, ekran kilitlenince durma.
- **Arayüz (UI Automation; fare ve klavye kullanılmadı):**
  - Makrolarım: arama, çoğalt, yeniden adlandır, çöp kutusuna sil, çöp kutusunu göster, geri yükle.
  - Ana Sayfa → Tümünü gör.
  - İçe aktarma: dosya seçildi, onay penceresi adımları ve "Excel Testi (2)" önerisini gösterdi, içe aktarıldı ve yeni kimlik aldı.
  - Test kopyaları sonra silindi.
- **Test edilmedi:**
  - Dışa aktarma penceresi (`File.Copy`).
  - Gerçek pencerede boyut geri getirme ve gerçek kilitli ekranda `SendInput` davranışı: ikisi de oynatma ister, kullanıcı bilgisayardayken çalıştırılmadı.
  - **Bitti ölçütü (Win10 ve Win11, %100 ve %150 ölçek) karşılanmadı:** bu makine yalnızca Win10, iki monitör de %100. Ölçeği değiştirmek sistem ayarı olduğu için yapılmadı.

---

## Aşama 4 — Düzenleyici (2026-10-15)

### Yapı

- **Core:** `DelayParser` (bekleme metni → ms), `StepWarnings` (adım uyarıları), `MacroLibrary.Save` (önceki sürümü saklayarak üzerine yazma), `VersionsOf`.
- **Engine:** `StepListEditor` (adım listesi + geri al/yinele, saf mantık, testli). `PlaybackOptions.FirstStep/LastStep` ("buradan oynat", "bu adımı dene").
- **Native:** `WindowInterop.TopLevelWindowAt` (tıklama ekleme için imlecin altındaki pencere).
- **App:** `EditorPage` + `EditorViewModel` + `EditorRow`, `InputDialog` (metin veya listeden seçim soran küçük pencere), `PlaybackChoices` (bar ve düzenleyici aynı tekrar/hız seçeneklerini kullanıyor), Ana Sayfa'daki son makrolara "Düzenle" butonu.

### Kararlar

1. **Geri al/yinele yalnızca adımları kapsıyor.** Ad, tekrar ve hız tek alanlık değerler ve ekranda görünüyor; onları geri al yığınına katmak kodu büyütürdü. [Tahmin] Sınır 200 adım (prompt: en az 100).

2. **"Buradan oynat" ve "Bu adımı dene" kaydedilmemiş hâli oynatıyor.** Değişiklik dosyaya yazılmadan denenebilsin diye. "Buradan oynat"ta yalnızca ilk tur seçili adımdan başlıyor, sonraki turlar 1. adımdan. "Bu adımı dene" yalnızca o adımı bir kez oynatıyor. Ana pencere oynatma boyunca simge durumuna küçülüyor, bitince geri geliyor.

3. **Önceki sürümler:** her kayıtta eski dosya `macros\.history\<ad>\<zaman>.msmacro` olarak saklanıyor, son 10'u tutuluyor. "Önceki sürüme dön" en yeni saklanan sürümü geri alınabilir bir değişiklik olarak getiriyor; kaydedilene kadar dosyaya yazılmıyor. Prompt 7.5'teki `File.Replace` yerine `.tmp` + `File.Move(üzerine yaz)` kullanıldı: [Muhtemel] aynı diskte o da atomik ve hedef dosyanın önceden var olmasını istemiyor.

4. **Tablonun satırları yerinde güncelleniyor** (N. satır hep N. adımı gösteriyor). Her değişiklikte liste yeniden kurulsaydı, bekleme kutusundan Tab ile not kutusuna geçen kullanıcının imleci kaybolurdu.

5. **Bekleme hücresi:** `230`, `230 ms`, `0,23 sn`, `0.23 s` kabul ediliyor (en fazla 1 saat). Geçersiz bir şey yazılırsa eski değer geri geliyor. Değer kutudan çıkınca, Enter'a veya Ctrl+S'ye basınca alınıyor.

6. **Adım ekleme:** yeni adım seçimin arkasına (seçim yoksa sona) giriyor ve önündeki adımın penceresini hedef alıyor. "Tıklama" 3 saniye geri sayıp imlecin o anki yerini ve altındaki pencereyi alıyor. Masaüstü/görev çubuğu tıklamaları kayıttaki gibi ekran konumuyla oynatılıyor. "Tuş" sabit bir listeden seçiliyor (Enter, Tab, Esc, Boşluk, Geri, Delete, oklar, Home/End, PgUp/PgDn, F2, F5). Kısayollar ve diğer tuşlar için kayıt daha güvenilir.

7. **Uyarılar:** Excel'de dikey tekerlek ve dönüştürülmemiş Alt+Tab satırda ⚠ ile gösteriliyor. Prompt 7.3'teki "kayıt çözünürlüğü farklı" uyarısı tek tek adımlara değil tüm makroya ait. Bu yüzden sayfanın üstünde bir şerit oldu (çözünürlük veya ölçek ana ekrandan farklıysa). Excel tekerleği "Diğer" menüsünden ok tuşlarına çevrilebiliyor (kayıttaki dönüşümle aynı kod).

8. **Kaydedilmemiş değişiklikler** başka makro seçilirken ve uygulamadan çıkarken soruluyor (Kaydet / Kaydetme / Vazgeç). Pencereyi kapatmak değişiklikleri kaybettirmiyor, çünkü pencere yalnızca gizleniyor.

9. **Çoklu seçim:** sil, çoğalt, aç/kapat ve bekleme ayarla tüm seçili adımlara uygulanıyor. Yukarı/aşağı taşı ve oynatma komutları ilk seçili adımı kullanıyor.

10. **Menüden açılan düzenleyici en yeni makroyu gösteriyor**, boş sayfa yerine. "Düzenlenecek makro yok" yalnızca hiç makro yokken görünüyor.

11. **Simgeli butonların erişilebilir adı ipucu metni.** UI Automation ile yapılan testte bu butonların adı boş çıktı; ekran okuyucu da aynı boşluğu okurdu.

### Testler

- **Birim testleri:** 128 testin 128'i geçiyor. Yeni testler: bekleme metni ayrıştırma, adım uyarıları, sürüm saklama, düzenleyicinin tüm işlemleri ve geri al/yinele, oynatıcının ilk/son adım sınırları. TR/EN kaynak eşliği de testli.
- **Arayüz (UI Automation, fare/klavye kullanılmadan):**
  - Ana Sayfa → "Düzenle" düzenleyiciyi açtı.
  - 2. adımın beklemesine `1,5 sn` yazıldı, tabloda `1500` oldu ve kaydedilince dosyaya `1500` yazıldı. Önceki sürüm `.history`'ye düştü.
  - "Önceki sürüme dön" eski değerleri getirdi ve kaydedildi.
- **Test edilmedi:** sürükle-bırak, ekleme pencereleri, klavye kısayolları ve "Buradan oynat"/"Bu adımı dene". Bu sonuncular fareyi ve klavyeyi kullanıyor; kullanıcı bilgisayardayken çalıştırılmadı. Düzenlenmiş dosyayı oynatan kod Aşama 3'teki testli oynatıcı. [Muhtemel] Bitti ölçütünün "tekrar oynatılabiliyor" kısmı bu yüzden gerçek Excel'de doğrulanmadı.

---

## Aşama 3 — Oynat (2026-10-15)

### Yapı

- **Core:** oynatma sözleşmeleri (`IInputSimulator`, `IWindowSystem`, `IClipboard`, `IUserActivityMonitor`, `IPlaybackClock`), `PlaybackOptions/Result/Progress`.
- **Engine:** `MacroPlayer` (saf mantık, sahte bir masaüstüyle testli), `WindowMatcher`, `PositionMapper`, `StopwatchClock`.
- **Native:** `InputSimulator` (`SendInput`), `WindowSystem`, `ClipboardAccess` (Win32 pano), `UserActivityMonitor` (oynatma kancaları), `TimerResolution`. Kayıt ve oynatma kancaları ortak `HookThread` ve `ModifierTracker` kullanıyor.
- **App:** `PlaybackController`, `PlaybackNotifier`, bar düğmeleri ve ilerleme metni, Ayarlar > Oynatma.

### Kararlar

1. **Pencere eşleştirme: önce program + pencere türü, başlık yalnızca adaylar arasında.** [Kesin] Başlıklar değişiyor (Not Defteri `*` ekliyor, Excel dosya adını gösteriyor). En benzer başlık "düzenleme mesafesi" ile seçiliyor; eşitlikte en öndeki pencere kazanıyor. İstisna standart iletişim kutuları (`#32770`): hepsi aynı türde olduğu için başlık benzerliği en az %60 olmalı, yoksa "Farklı Kaydet" beklenirken açık duran başka bir iletişim kutusu kullanılırdı.

2. **Fare konumları pencereyle birlikte taşınıyor.** Konum = kayıttaki ekran noktası − kayıttaki pencere köşesi + şimdiki pencere köşesi. Pencere boyutu değiştiyse sağa/alta yapışık öğeler kayabilir. [Tahmin] Bunun çözümü UI Automation ile öğeyi adından bulmak; Aşama 5'te değerlendirilecek.

3. **Duraklama yalnızca adımlar arasında.** Bir adımın içindeki kısa beklemeler (tuşa basıp bırakma arası 15 ms gibi) durdurulabiliyor ama duraklatılamıyor. Duraklatılmış makro hiçbir tuşu basılı bırakmıyor. Durdurmada `ReleaseAll` basılı her şeyi bırakıyor (testli).

4. **"Boşsa dur" her turun ilk kopyalamasına bakıyor.** Örnek senaryoda iki kopyalama var: Excel'den veri, hedef programdan sonuç. İkincisi boş geldiğinde (sonuç alanı boş) liste bitmiş sayılmamalı.

5. **Kopyalama kontrolü pano sayacıyla** (`GetClipboardSequenceNumber`). Prompt `AddClipboardFormatListener` diyordu; o bir pencere ister, sayaç istemez ve oynatma thread'inden okunabiliyor. 2 sn içinde pano değişmezse adım "Hiçbir şey kopyalanmadı" hatasıyla duruyor.

6. **Temiz yapıştırma panoya düz metin yazıyor.** Excel'in fazladan satır sonu bu sayede gidiyor; bedeli zengin biçimin (renk, formül) yapıştırılmaması (prompt 7.4'e eklendi).

7. **Pencereyi öne getirme:** önce `SetForegroundWindow`. Windows izin vermezse öndeki programın girdi durumu kısa süre paylaşılıyor (`AttachThreadInput`); bunun için oynatma thread'inde bir mesaj kuyruğu oluşturuluyor. Alt tuşuna basma hilesi kullanılmadı, çünkü hedef programda menüyü açar.

8. **Yönetici olarak çalışan hedef** (UIPI) oynatmadan önce algılanıyor ve açık bir mesajla duruyor. Windows bu programlara giden tuşları sessizce yutar; algılanmasaydı makro "çalıştı" görünüp hiçbir şey yapmazdı.

9. **Esc oynatma sırasında programa ulaşmıyor.** Makroyu durdurmak için basılan Esc, hedef programda bir iletişim kutusunu da kapatmamalı.

10. **Ctrl+Alt+End yalnızca oynatma boyunca kayıtlı.** [Kesin] Uzak Masaüstü pencerelerinde Ctrl+Alt+Del yerine geçer; sürekli kayıtlı tutmak o kullanımı bozardı.

11. **Devam'dan sonraki 1 saniye müdahale sayılmıyor.** Bardaki Devam'a tıklayan kullanıcının fareyi bırakırken yaptığı hareket makroyu yeniden duraklatırdı.

12. **Zamanlama:** oynatma boyunca Windows zamanlayıcı çözünürlüğü 1 ms'ye alınıyor (`timeBeginPeriod`). Bekleme, son milisaniye dışında uyuyarak geçiyor ve durdurma isteğinde hemen uyanıyor.

13. **Adım içi sabit aralıklar** (hızdan etkilenmez): fare yerleşme 20 ms, tuş basılı tutma 30 ms, çift tık arası 60 ms, tuş arası 15 ms, karakter arası 4 ms, tekerlek tıkı arası 20 ms. Sürüklemede uzun atlamalar 20 px'lik ara noktalara bölünüyor, çünkü programlar sürüklemeyi ancak basılıyken hareket görünce başlatıyor.

14. **Hata bildirimi:** normal bitiş, liste sonu ve durdurma tepside balonla bildiriliyor. Hata ise her pencerenin üstünde bir mesaj kutusuyla, ne yapılacağını söyleyerek gösteriliyor ("Şu pencere bulunamadı: … Program açık mı?").

### Testler

- **Birim testleri:** 98 testin 98'i geçiyor. Oynatıcı için sahte masaüstüyle: "boşsa dur", N tur, hız, taşınan pencere, eksik pencere, yönetici hedef, değişmeyen pano, temiz yapıştırma, devre dışı adım, durdurmada tuş bırakma, parola duraklaması, ilerleme bildirimi. Pencere eşleştirici için: değişen başlık, birden çok pencere, başka program, yanlış iletişim kutusu.
- **Excel ile uçtan uca test:** betik hazır (Excel → Not Defteri, 50 satır, 1x ve 2x). Kullanıcı bilgisayarı kullanırken çalıştırılmadı, çünkü test fareyi ve klavyeyi ~3 dakika tamamen kullanıyor.

---

## Aşama 2 — Kaydet (2026-10-15)

### Yapı

- **Core:** Makro modeli ve 10 adım türü, `MacroLibrary` (`.msmacro` dosyaları), `MacroValidator` (sınırlar), `ExcelWheelAdvice`, kayıt sözleşmeleri (`IInputCapture`, `IWindowInspector`, `IFocusInspector`).
- **Engine:** `StepBuilder` (ham olaylar → adımlar; Windows'a dokunmaz, testli), `MacroRecorder` (kuyruk + arka plan thread'i), `PathSimplifier`.
- **Native:** `InputCapture` (kancalar kendi thread'inde), `WindowInspector`, `FocusInspector` (UI Automation; Native projesine yalnızca bunun için `UseWPF` eklendi).
- **App:** `RecordingController`, geri sayım penceresi, kaydetme penceresi, Ayarlar > Kayıt, Ana Sayfa'da son makrolar.

### Kararlar

1. **Kaydedilen modellerde `init` yerine `set`.** [Kesin]
   System.Text.Json kaynak üreteci, dosyada olmayan `init` özelliklerini kodda verilen varsayılana değil `null`/`0`/`false`'a çekiyor. Testle görüldü: alanları eksik bir ayar dosyası barı gizli açtı; `isEnabled` alanı olmayan bir makro adımları devre dışı yüklerdi. Bu, Aşama 1'deki ayar dosyasını da etkileyen gizli bir hataydı. Düzeltildi, `MissingFieldsTests` korur.

2. **Prompt'taki çelişki: kayıt sırasında bar.** Prompt hem "kayıt/oynatmada bar açılmaz" hem "kayıt bar → Durdur ile biter" diyordu. Karar: kayıt başlarken bar kapanır (sabitli olsa bile), fare üzerinde durursa açılır; bara yapılan tıklamalar kayda girmez. Oynatmada bar açılmaz (Aşama 3). Prompt 5.2 güncellendi.

3. **Kapalı bar fareye tamamen geçirgen.** [Kesin] `WindowFromPoint` katmanlı pencerenin şeffaf piksellerini de pencerenin parçası sayar; kapalı barın 640×48'lik alanına yapılan tıklamalar "Macro Studio'ya tıklandı" diye kayıttan atılırdı. Kapalıyken `WS_EX_TRANSPARENT`, açıkken kaldırılıyor.

4. **Programla gönderilen girdi de kaydediliyor.** [Muhtemel] Ekran klavyesi, uzak masaüstü araçları ve Unicode giriş (`VK_PACKET`) kayda girmeli. Oynatma sırasındaki "kullanıcı müdahalesi" ayrımı (`LLMHF_INJECTED`) Aşama 3'te.

5. **Fare hareketleri yalnızca sürükleme sırasında** (bir tuş basılıyken) kuyruğa giriyor. Prompt 9'daki "fare hareketlerini kaydet" ayarı kaldırıldı: üzerine gelince açılan menüler ofis programlarında tıklamayla da açılıyor ve saniyede yüzlerce olay kuyruğu doldururdu.

6. **Süre eşikleri** [Tahmin, testlerde sabit]:
   - **Yazma:** 1 sn'den uzun duraklama yeni bir metin adımı başlatır.
   - **Aynı tuş:** 1 sn içinde yeniden basılırsa ×N olarak birleşir.
   - **Tekerlek:** 400 ms içindeki tıklar tek adımda birleşir.
   - **Pencere geçişi:** pencere bir tıklamadan sonraki 1 sn içinde öne gelirse geçişi o tıklama yapmış sayılır.
   - **Görev çubuğu:** tıklamadan sonraki 1 sn içinde öne gelen pencere "öne getir" adımı olur.
   - **Sürükleme eşiği:** Windows'un `DragSize` değeri (%100'de 4 px).

7. **İlk adımın beklemesi 0.** Geri sayım bittikten sonra kullanıcının pencereye geçme süresi oynatmada boşuna beklenmez.

8. **Görev çubuğu ile masaüstü ayrımı.** Görev çubuğu tıklaması pencere değiştirirse "öne getir" adımına dönüşür. Masaüstü tıklamaları (ör. bir simgeye çift tık) ekran konumuyla kalır; açılan program ayrıca "öne getir" adımı olarak kaydedilir.

9. **Tek başına basılan değiştirici tuşlar.** Alt (Excel şerit harfleri) ve Windows (Başlat) kaydedilir; tek başına Ctrl ve Shift bir şey yapmadığı için kaydedilmez. Caps Lock, Num Lock, Scroll Lock da kaydedilmez, çünkü yazılan metin onların etkisini zaten içeriyor.

10. **AltGr** Windows'ta Ctrl+Alt olarak geliyor. Ctrl+Alt ile basılan tuş yazdırılabilir bir karakter üretiyorsa (Türkçe Q'da AltGr+Q = @) bu metin sayılıyor, kısayol değil.

11. **Klavye düzeni ve Caps Lock.** Karakter, öndeki pencerenin klavye düzeniyle `ToUnicodeEx` üzerinden çevriliyor; "durumu değiştirme" bayrağıyla, böylece kullanıcının ölü tuşları bozulmuyor. Caps Lock durumu her tuşta Windows'tan okunuyor (bu thread'den doğru okunduğu ölçüldü). Kayıt başlarken zaten basılı olan değiştirici tuşlar `GetAsyncKeyState` ile okunuyor.

12. **Parola alanları.** Odaktaki kontrol UI Automation ile soruluyor. Bu kontrol kancanın içinde değil kuyruğu okuyan thread'de yapılıyor, çünkü kanca yavaşlarsa Windows onu kaldırır. Parola alanında yazılan karakterler ve Backspace hiç tutulmuyor; alan başına bir "Parola alanı – elle girin" adımı ekleniyor.

13. **Excel tekerlek önerisi kaydetme penceresinde.** Uyarı metni ve "Tekerleği ↓ tuşuna çevir" butonu orada. Çevirme, Windows'un "tek seferde kaydırılacak satır" ayarını kullanıyor (varsayılan 3). [Kesin] Bu makinede Excel yok; bu yol yalnızca birim testleriyle doğrulandı.

14. **Dosya adı makronun adından.** Geçersiz karakterler temizleniyor, CON/NUL gibi Windows'a ayrılmış adlara `_` ekleniyor, aynı ad varsa " (2)" ekleniyor. "Her kayıtta önceki sürümü sakla" kuralı Aşama 4'e (düzenleyici) kaldı: Aşama 2 yalnızca yeni dosya oluşturuyor, üzerine yazmıyor.

15. **Kaydetme penceresi kapatılınca soru soruyor.** "Kaydetme", X ve Alt+F4 için "Kaydetmeden kapatılsın mı?" soruluyor. İlk sürümde soru kapanmakta olan pencereden açıldığı için WPF hata veriyordu (uçtan uca testte bulundu). Kapanma şimdi önce iptal ediliyor, soru hemen arkasından soruluyor.

### Testler

- **Birim testleri:** 78 testin 78'i geçiyor (Core 45, Engine 33). `StepBuilder` için tıklama/çift tık/sürükleme, yazma/Backspace/AltGr, Ctrl+C/V, Alt+Tab, görev çubuğu, parola, gecikmeler ve prompt'taki örnek senaryo var.
- **Uçtan uca test (gerçek kancalar, iki Not Defteri):** Ctrl+Home, Shift+End, Ctrl+C, Alt+Tab, tıklama, Ctrl+V, Enter, "abc" + Unicode "Tamam ğüşıöçİ", Backspace, Alt+Tab, ↓×3, iki tekerlek tıkı → beklenen 11 adımın 11'i, doğru sırayla. Ctrl+Alt+R kayda girmedi.
- **Bu testten kalan:** `%LocalAppData%\MacroStudio\macros\Makro 1.msmacro` (Not Defteri test kaydı) incelemek için bırakıldı; silinebilir.

### Aşama 3 için not

- [Kesin] Pencere başlıkları değişiyor (Not Defteri değişiklikte `*Hedef.txt` yazıyor). Oynatmada pencere önce program + sınıf ile bulunmalı, başlık yalnızca adaylar arasında ayırt etmek için kullanılmalı (prompt 7.4'e eklendi).

---

## Aşama 1 — GUI (2026-10-14)

### Paketler

| Paket | Neden |
|---|---|
| `WPF-UI` 4.3.0 | Win11 Fluent görünümü, Mica, Win10'da düz arka plana geri dönüş, `NavigationView`, `CardControl`, `ToggleSwitch`. Elle yazılacak yüzlerce satır stil yerine. |
| `CommunityToolkit.Mvvm` 8.4.2 | `[ObservableProperty]` / `[RelayCommand]` kaynak üreteciyle az kod. |
| `Microsoft.Extensions.DependencyInjection` 10.0.13 | Tüm parçaların tek yerde (`App.xaml.cs`) birbirine bağlanması. |
| `Serilog` 4.4.0 + `Serilog.Sinks.File` 7.0.0 | Günlük dönen log dosyası, 14 gün saklama. |
| `xunit.v3` 4.0.2 | Birim testleri (Microsoft.Testing.Platform ile). |

### Prompt'tan sapmalar

1. **`Microsoft.Extensions.Hosting` yerine yalnızca `Microsoft.Extensions.DependencyInjection`.** [Muhtemel]
   Host'un yapılandırma dosyası, ortam değişkeni ve arka plan servis özellikleri bu uygulamada kullanılmıyor. Gereksiz kod/bağımlılık olurdu.

2. **Serilog doğrudan kullanılıyor; `Microsoft.Extensions.Logging` köprüsü yok.** [Tahmin]
   Aşama 1'de log yazan tek yer `App.xaml.cs`. Aşama 2'de Engine projesi log yazacağı zaman `Microsoft.Extensions.Logging.Abstractions` eklenecek, Engine Serilog'a bağımlı olmayacak.

3. **İkonlar WPF-UI'nin gömülü Fluent System Icons fontundan (`SymbolIcon`).** [Kesin]
   Prompt `Segoe Fluent Icons, Segoe MDL2 Assets` font zincirini söylüyordu. Gömülü font Win10 ve Win11'de birebir aynı ikonları verir, Windows'ta hangi fontun kurulu olduğuna bağlı değildir.

4. **Çentik bar açık genişliği 640 DIP** (prompt: ~560; sabitle butonuyla 680, aşağıya bakın). [Kesin]
   Ev + kaydet/oynat/durdur + makro seçici + tekrar + hız + durum metni, İngilizce metinlerle ("Until empty") 560'a sığmadı. Ölçüldü: 96 DIP'lik tekrar kutusunda "Until empt" kesiliyordu; 116 DIP'e çıkarıldı.

5. **Kapalı barın fareyle açılma bölgesi:** şeridin kendisi (160 × 6 DIP) + iki yanda 40 DIP. [Tahmin]
   Prompt dikeyde "en üst 4 px" diyordu; şerit 6 DIP olduğu için şeridin tamamı sayıldı.

6. **Fare algılama 50 ms'de bir imleç konumu sorgulanarak yapılıyor.** [Muhtemel]
   Barın şeffaf kısımları tıklamaları alttaki pencereye geçirir; bu yüzden pencere farenin geldiğini kendisi göremez. Görünmez bir "yakalama alanı" eklemek, o alanın altındaki tarayıcı sekmelerine tıklamayı engellerdi. Ölçülen boşta CPU: %0,004–0,012.

7. **Tam ekran algılama:** ön plandaki pencere, barın bulunduğu monitörü piksel piksel tam kaplıyorsa bar gizlenir. [Muhtemel]
   `SHQueryUserNotificationState` hangi monitörde olduğunu söylemez. Ekranı kaplayan (maximize) pencereler gizli kenarları yüzünden monitörden 8 px taşar, bu yüzden tam ekran sayılmaz. Test edildi: kenarlıksız tam ekran pencere açılınca bar gizlendi, kapanınca geri geldi.

8. **Sol / Sağ konum:** barın merkezi monitör genişliğinin 1/4 ve 3/4 noktasına gelir. [Tahmin]
   Tam köşeye yaslamak pencerelerin dosya menüleriyle ve kapatma butonlarıyla çakışırdı.

9. **Bar monitörün *çalışma alanının* üstüne yapışır.** [Tahmin]
   Görev çubuğu ekranın üstündeyse bar onun altında durur, görev çubuğunu kapatmaz.

10. **Kaydet / Oynat / Durdur ve makro seçici Aşama 1'de devre dışı.**
    Arkalarında sahte kod yok; Aşama 2–3'te bağlanacaklar. Tekrar ve hız seçicileri şimdilik yalnızca seçimi tutuyor.

11. **Ayarlar sayfasında şimdilik yalnızca Genel ve Çentik Bar bölümleri var.**
    Kısayollar, Kayıt, Oynatma ve Veri bölümleri ilgili aşamada eklenecek. İşe yaramayan ayar göstermemek için.

12. **"Windows ile başlat" settings.json'da değil, yalnızca kayıt defterinde (`HKCU\...\Run`) tutuluyor.**
    Tek doğruluk kaynağı. Kullanıcı Görev Yöneticisi'nden kapatsa bile iki yerin birbiriyle çelişmesi önlenir.

13. **Sistem tepsisi simgesi WinForms `NotifyIcon`, menüsü ise WPF `ContextMenu`.** [Kesin]
    WPF'te tepsi simgesi yok. WinForms menüsü koyu temaya uymadığı için menü, barın sağ tık menüsüyle aynı WPF menüsü. WinForms'un `WFO0003` uyarısı kapatıldı: bu bir WPF uygulaması ve DPI ayarının doğru yeri `app.manifest`.

14. **Testler Microsoft.Testing.Platform ile çalışıyor (`global.json`).** [Kesin]
    .NET 10 SDK, xunit.v3 için eski VSTest yolunu artık kabul etmiyor. `Microsoft.NET.Test.Sdk` ve `xunit.runner.visualstudio` paketlerine gerek kalmadı.

15. **TR/EN metin dosyası testi `MacroStudio.Core.Tests` içinde.** [Tahmin]
    Yalnızca bu test için ayrı bir App test projesi açmak gereksizdi. Test dosyaları XML olarak okur, App'e referans vermez.

### Ölçümler ve karşılanmayan hedef

| Ölçüt (prompt 5.3) | Sonuç |
|---|---|
| Win10, %100 ölçek, şekil düzgün | Geçti (ekran görüntüsüyle) |
| %150 / %200 ölçek | **Test edilmedi:** tek monitör %100; ölçeği değiştirmek sistem ayarı |
| İki monitör, farklı ölçek | **Test edilmedi:** makinede tek monitör var |
| Not Defteri'ndeyken bara tıklayınca odak Not Defteri'nde kalıyor | Geçti |
| Tam ekran uygulamada bar gizleniyor | Geçti (kenarlıksız tam ekran pencereyle) |
| Boşta CPU < %0,5 | Geçti: Release'te %0,012 (20 çekirdeğin toplamı) |
| Bellek < 120 MB | **Geçmedi:** Release'te working set 141–145 MB, private 82–84 MB |

**Bellek hakkında:** Ana pencereyi ilk açılışa kadar oluşturmamak denendi; yalnızca 4 MB kazandırdı ve bir geçici çözüm gerektirdi, geri alındı. Belleğin büyük kısmı WPF'in kendisi ve WPF-UI'nin stil sözlükleri. [Muhtemel] Hedefi "private < 100 MB" olarak güncellemek gerçekçi olur; karar proje sahibinde.

### Düzeltme: sayfalar kaymıyordu (2026-10-15)

- **Sebep:** [Kesin] WPF-UI her `Page`'i kendi `DynamicScrollViewer`'ının içine koyuyor (`Page` için `ScrollViewer.CanContentScroll` varsayılanını `true` yapıyor). Sayfalara ayrıca eklenen `ScrollViewer` sınırsız yükseklik aldığı için hiç kaydırmıyor, üstelik tekerlek olayını "işlendi" diye yutuyordu. Bu yüzden dıştaki de kaydırmıyordu. Beş sayfanın hepsinde vardı.
- **Çözüm:** Sayfalardaki `ScrollViewer` kaldırıldı. Bütün sayfalar `ContentPage`'den türüyor. `ContentPage` açıldığında ve boş yerine tıklandığında klavye odağını alıyor, böylece ok tuşları, Page Up/Down, Home ve End sayfayı kaydırıyor.
- **Kural:** İleride bir sayfa kendi kaydırmasını yönetecekse (ör. Düzenleyici'deki adım tablosu), sayfada `ScrollViewer.CanContentScroll="False"` ayarlanır. WPF-UI bunu görünce dış kaydırma alanını kapatır.

### Karar: kapalı açılır liste fare tekerleğini almaz (2026-10-15)

[Kesin] WPF'te odaklı, kapalı bir `ComboBox` tekerlekle değerini değiştirir. Test edildi: sayfa kaydırılırken tema "Koyu"dan "Açık"a geçti, bardaki tekrar sayısı "Boşsa dur"dan "1"e geçti; ikisi de kullanıcı fark etmeden. `ComboBoxWheelBehavior` uygulamadaki bütün açılır listeler için bunu kapatıyor: kapalı liste tekerleği üstteki öğeye iletir, sayfada bu sayfayı kaydırır. Açık listenin içinde tekerlek normal çalışır. Win11 Ayarlar uygulaması da böyle davranır.

### Karar: barı açık sabitleme butonu (2026-10-15)

- Proje sahibinin isteği: fare bardan çekilince bar kapanıyor; isteyen açık tutabilsin.
- Barın en sağında 📌 butonu. Basılınca bar açık kalır, tekrar basınca normal davranışa döner. Durum `settings.json` içinde `notchBarPinned` olarak saklanır. Eski ayar dosyalarında bu alan yoksa bar sabitsiz başlar (testli).
- Bar genişliği bu buton için önce 640 → 680 DIP oldu; "Hazır" yazısı kaldırılınca yeniden **640 DIP**.
- [Kesin] Sabitli bar her zaman en üstte duran 680 × 48 DIP'lik bir alandır; altındaki pencerelerin üst-orta kısmına tıklanamaz. Kullanıcı barı Konum ayarından sola/sağa alabilir.
- Tam ekran uygulamada, pencere sürüklenirken ve "1 saat gizle" süresince sabitli bar da gizlenir. Aşama 2–3'te kayıt ve oynatma sırasında da kapanacak (prompt 5.2'ye eklendi).

### Karar: bardaki "Hazır" yazısı kaldırıldı (2026-10-15)

- Her zaman "Hazır" yazıyordu ve hiçbir bilgi vermiyordu.
- [Kesin] Kayıt ve oynatma sırasında bar kapalı kaldığı için canlı ilerleme ("Döngü 4/10") zaten orada görünemez. Durum metni Aşama 3'te geri gelecek ve yalnızca duraklatıldığında, iş bittiğinde veya hata olduğunda görünecek (prompt 5.1 güncellendi).
- Yerine sabitle butonunun önüne bir ayırıcı çizgi kondu.

### Bilinen kısıtlar

- **Kenar menü öğeleri UI Automation "Invoke" desteği vermiyor** (WPF-UI `NavigationViewItem`). Fare ve klavyeyle çalışıyorlar. Ekran okuyucu desteği Aşama 6'da ele alınacak.
- **`Ctrl+Alt+M` şimdilik sabit.** Değiştirilebilir kısayollar Ayarlar > Kısayollar bölümüyle birlikte gelecek.
- **`%LocalAppData%\MacroStudio\logs` klasöründe eski bir Macro Studio sürümüne ait log dosyaları var.** Yeni sürüm aynı klasörü ve aynı exe adını kullanıyor. Eski sürüm hâlâ kuruluysa kaldırılmalı; kısayol, başlangıç kaydı ve dosya ilişkilendirmesi çakışabilir.
