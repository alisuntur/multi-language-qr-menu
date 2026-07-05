# Proje 3 — QR Menü + Çok Dilli Restoran Paneli

## Proje Tanımı

Bu proje restoran, kafe, beach club, otel restoranı ve benzeri işletmelerin menülerini QR kod üzerinden çok dilli, mobil uyumlu ve kolay güncellenebilir şekilde sunmasını sağlar. İşletme panelden kategori, ürün, fiyat, görsel, alerjen, kampanya ve tema ayarlarını yönetir. Müşteri QR kodu okuttuğunda public menü sayfası açılır.

Ürün üç proje arasında en hızlı geliştirilebilecek ve en hızlı satılabilecek üründür. İlk hedef; işletmeye kolay kurulabilir, düşük bakım gerektiren, tekrar satılabilir bir QR menü SaaS/kurulum paketi oluşturmaktır.

## Hedef Müşteri

| Müşteri Tipi | İhtiyaç | Ürünün Çözümü |
|---|---|---|
| Restoran | Menü fiyatlarını hızlı güncellemek | Panelden anlık ürün/fiyat yönetimi |
| Kafe | QR menü ve kampanya göstermek | Mobil public menü ve öne çıkan ürünler |
| Beach club | Turiste çok dilli menü sunmak | TR/EN/DE/RU dil desteği |
| Otel restoranı | QR menü + oda/misafir bağlantısı | Masa veya genel QR kullanımı |
| Bar | Güncel içecek/menü fiyatları | Kategori ve ürün bazlı hızlı güncelleme |

## Kullanıcı Rolleri

| Rol | Yetki Özeti |
|---|---|
| Sistem Admini | Tüm restoranları, paketleri ve sistem ayarlarını yönetir. |
| Restoran Sahibi | Restoran ayarları, tema, kullanıcılar ve tüm menüyü yönetir. |
| Şube Müdürü | Menü, kampanya ve fiyatları yönetir. |
| Menü Editörü | Sadece kategori/ürün/fiyat/görsel alanlarını düzenler. |
| Müşteri | QR menüyü public olarak görüntüler. |

## Önerilen Teknoloji

| Katman | Teknoloji | Not |
|---|---|---|
| Frontend | React + Vite + TypeScript | Admin panel + public QR menü |
| Backend | ASP.NET Core Web API veya FastAPI | Basit SaaS API yapısı |
| Veritabanı | PostgreSQL / Supabase PostgreSQL | Restoran bazlı veri izolasyonu |
| Auth | JWT + Refresh Token | Panel için gerekli |
| Dosya | Supabase Storage / S3 | Logo, ürün görseli, kapak görseli |
| QR/PDF | Backend veya frontend PDF üretimi | Masa bazlı QR çıktısı |
| Analitik | Anonim event kayıtları | Kişisel veri toplamadan basit istatistik |

## MVP Kapsamı

| Modül | MVP'de Durum | Açıklama |
|---|---|---|
| Restoran hesabı/auth | Olmalı | Yönetim paneli için şart |
| Kategori yönetimi | Olmalı | Menü yapısının temeli |
| Ürün yönetimi | Olmalı | Ana işlev |
| Public QR menü | Olmalı | Müşterinin gördüğü ürün |
| Çok dil | Olmalı | Turistik bölgede satış değeri |
| QR kod üretimi | Olmalı | Fiziksel masalara koymak için şart |
| Tema/marka ayarı | Olmalı | Her işletmeye satılabilir görünüm |
| Kampanya/öne çıkanlar | MVP+ | Satış değerini artırır |
| Alerjen/etiket | MVP+ | Profesyonel görünüm sağlar |
| Basit istatistik | MVP+ | İşletmeye değer katar |
| Excel import/export | Sonraki faz | Çok ürünlü işletmeler için kolaylık |

## Kapsam Dışı İlk Sürüm Maddeleri

| Kapsam Dışı | Neden Dışarıda? |
|---|---|
| Online sipariş | QR menüyü restoran POS sistemine dönüştürür. |
| Masa sipariş yönetimi | Operasyon karmaşıklığı artar. |
| Online ödeme | İlk sürüm için menü görüntüleme yeterlidir. |
| POS entegrasyonu | Her işletmede farklı sistem olur. |
| Kurye/paket servis altyapısı | Ayrı ürün kapsamıdır. |


## Genel Ekip Çalışma Akışı

| Sıra | Aşama | Açıklama | Çıktı | Kontrol |
|---:|---|---|---|---|
| 1 | Sprint kapsamı netleştirme | Sprintte yapılacak modüller, sınırlar ve bağımlılıklar belirlenir. | Sprint backlog | Ekip sprint hedefini aynı şekilde anlamalıdır. |
| 2 | Veritabanı tasarımı | Tablolar, ilişkiler, enum değerleri, indeksler ve migration planı çıkarılır. | ERD/migration taslağı | Backend başlamadan önce tablo ilişkileri onaylanır. |
| 3 | Backend DTO/API sözleşmesi | Request/response DTO'ları, endpoint adları ve validation kuralları belirlenir. | API sözleşmesi | Frontend aynı sözleşmeye göre ilerleyebilmelidir. |
| 4 | Backend geliştirme | Endpointler, servisler, repository/query katmanı ve iş kuralları yazılır. | Çalışan API | Swagger/Postman üzerinden test edilir. |
| 5 | Frontend ekran tasarımı | Liste, form, detay, modal, public ekran ve rol bazlı menüler yapılır. | UI ekranları | Responsive görünüm kontrol edilir. |
| 6 | API entegrasyonu | Frontend gerçek endpointlere bağlanır, loading/error/empty state eklenir. | Entegre ekran | Sahte veri kalmamalıdır. |
| 7 | Yetki ve güvenlik testi | Kullanıcı rolleri, veri izolasyonu ve hatalı erişimler test edilir. | Yetki test sonucu | Başka müşterinin verisi görünmemelidir. |
| 8 | Uçtan uca test | Gerçek kullanıcı senaryosu baştan sona çalıştırılır. | E2E test sonucu | Kritik akış manuel olarak tamamlanmalıdır. |
| 9 | Demo ve kabul | Sprint sonunda çalışan özellik ekip/müşteri gibi değerlendirilir. | Sprint demo | Kabul kriterleri sağlanmadan sprint kapanmaz. |
| 10 | Eksiklerin backlog'a alınması | Sprint dışı kalan, bug veya iyileştirme maddeleri kaydedilir. | Backlog güncellemesi | Yeni sprintte öncelik verilebilir olmalıdır. |

## Standart Görev Alanları

| Alan | İçerik |
|---|---|
| Backend | API endpointleri, iş kuralları, servisler, validation, auth, loglama, hata yönetimi |
| Frontend | Sayfalar, componentler, form validasyonları, responsive görünüm, public/admin akışları |
| Veritabanı | Tablolar, ilişkiler, enumlar, migration, index, seed data, soft delete alanları |
| Test | Unit test, API test, rol/yetki testi, manuel kabul testi, mobil test, veri doğruluğu testi |
| Dokümantasyon | Kurulum yönergesi, API notları, kullanıcı akışı, demo kullanıcıları, bilinen kısıtlar |

## Standart Kabul Kriterleri

| Kriter | Açıklama |
|---|---|
| Çalışır durum | Backend, frontend ve veritabanı aynı ortamda sorunsuz çalışmalıdır. |
| Gerçek veri | Frontend ekranları sahte veriyle değil API verisiyle çalışmalıdır. |
| Yetki kontrolü | Kullanıcı sadece rolünün ve işletmesinin izin verdiği verileri görmelidir. |
| Validation | Eksik, hatalı veya geçersiz veri backend tarafından reddedilmelidir. |
| Loglama | Kritik durum değişikliklerinde audit veya status log tutulmalıdır. |
| Mobil uyum | Public ekranlar özellikle telefonda sorunsuz açılmalıdır. |
| Demo edilebilirlik | Sprint sonunda çalışan özellik baştan sona gösterilebilmelidir. |


## Sprint 0 — Proje Kurulumu

| Alan | Açıklama |
|---|---|
| Sprint amacı | QR menü sistemi için backend, frontend, veritabanı ve standart proje yapısını kurmak. |
| Ana çıktı | Çalışan proje iskeleti, admin/public layout ve ilk migration. |
| Sprint kabulü | Ekip projeyi localde sorunsuz çalıştırabilmelidir. |

### Backend Görevleri

| Kod | Görev | Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S0-BE-01 | Backend iskeleti | Katmanlı API kurulumu. | Çalışan backend | Localde çalışmalı. |
| S0-BE-02 | Swagger | API dokümantasyonu. | Swagger UI | Açılmalı. |
| S0-BE-03 | Global response/exception | Standart API cevapları ve hata yakalama. | API standardı | Hatalar kontrollü dönmeli. |
| S0-BE-04 | Validation/CORS/logging | Temel backend altyapısı. | Geliştirme temeli | Frontend bağlanmalı. |
| S0-BE-05 | Health endpoint | GET /health. | Sistem durumu | 200 OK dönmeli. |

### Frontend Görevleri

| Kod | Görev | Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S0-FE-01 | React kurulumu | React + Vite + TypeScript. | Çalışan frontend | npm run dev. |
| S0-FE-02 | Admin layout | Panel ana layout. | Admin iskeleti | Menü/sidebar hazır. |
| S0-FE-03 | Public layout | QR menü mobil layout. | Public iskelet | Telefon görünümü düzgün. |
| S0-FE-04 | API client | Servis katmanı. | API bağlantısı | Health çağrısı yapılmalı. |

### Veritabanı Görevleri

| Kod | Tablo/İş | Alanlar/Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S0-DB-01 | DB bağlantısı | PostgreSQL/Supabase. | DB connection | Migration çalışmalı. |
| S0-DB-02 | İlk migration | restaurants, users, roles, audit_logs. | Temel tablolar | Seed roller hazır. |

### Test ve Kabul Senaryoları

| Kod | Senaryo | Adımlar | Beklenen Sonuç | Test Türü |
| --- | --- | --- | --- | --- |
| S0-QA-01 | Kurulum testi | Backend/frontend/DB birlikte çalışır. | Sistem ayakta | Manuel |
| S0-QA-02 | Public layout testi | QR boş sayfa mobil açılır. | Mobil görünüm düzgün | Responsive |


## Sprint 1 — Auth ve Restoran Hesabı

| Alan | Açıklama |
|---|---|
| Sprint amacı | Restoran yetkililerinin güvenli giriş yapıp kendi restoranını yönetmesini sağlamak. |
| Ana çıktı | Restoran hesabı, auth ve rol sistemi. |
| Sprint kabulü | Her restoran sadece kendi menüsünü yönetebilmelidir. |

### Backend Görevleri

| Kod | Görev | Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S1-BE-01 | Auth endpoints | login/logout/refresh/me. | JWT oturum | Doğru kullanıcı giriş yapmalı. |
| S1-BE-02 | Restoran current | GET/PUT /restaurants/current. | Restoran profili | Yetkili güncellemeli. |
| S1-BE-03 | Rol bazlı yetki | RESTAURANT_OWNER, BRANCH_MANAGER, MENU_EDITOR. | Authorization | Menü editörü ayar değiştirememeli. |
| S1-BE-04 | Restoran izolasyonu | restaurant_id filtresi. | Tenant güvenliği | Başka restoran verisi dönmemeli. |
| S1-BE-05 | Pasif restoran | is_active false ise public menü kapanabilir. | Yayın kontrolü | Pasif restoran publicte görünmemeli. |

### Frontend Görevleri

| Kod | Görev | Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S1-FE-01 | Login ekranı | E-posta/şifre. | Giriş UI | Hatalı girişte uyarı. |
| S1-FE-02 | Auth state | Token ve kullanıcı yönetimi. | Oturum yönetimi | Sayfa yenilenince korunmalı. |
| S1-FE-03 | Restoran ayarları | Ad, telefon, WhatsApp, logo, para birimi. | Ayar sayfası | Kaydetme çalışmalı. |
| S1-FE-04 | Rol bazlı menü | Sahip/müdür/editör ayrımı. | Dinamik menü | Yetkiye uygun görünmeli. |

### Veritabanı Görevleri

| Kod | Tablo/İş | Alanlar/Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S1-DB-01 | restaurants | name, slug, phone, whatsapp_phone, email, address, logo_url, default_language, currency, is_active. | Restoran tablosu | slug unique. |
| S1-DB-02 | users | restaurant_id, full_name, email, phone, password_hash, is_active. | Kullanıcı tablosu | Email unique. |
| S1-DB-03 | roles/user_roles | Rol ve ilişkiler. | Yetki tabloları | Roller seed edilmeli. |
| S1-DB-04 | refresh_tokens | Oturum tokenları. | Token tablosu | Logout revoke etmeli. |

### Test ve Kabul Senaryoları

| Kod | Senaryo | Adımlar | Beklenen Sonuç | Test Türü |
| --- | --- | --- | --- | --- |
| S1-QA-01 | Login | Restoran sahibi giriş yapar. | Panel açılır | E2E |
| S1-QA-02 | Rol testi | Menü editörü tema ayarına girer. | Engellenir | Yetki |
| S1-QA-03 | İzolasyon | Restoran A, B ürününü çağırır. | Engellenir | Güvenlik |


## Sprint 2 — Kategori Yönetimi

| Alan | Açıklama |
|---|---|
| Sprint amacı | Menü kategorilerinin oluşturulması, çevrilmesi ve sıralanmasını sağlamak. |
| Ana çıktı | Kategori yönetimi, sıralama ve çok dilli kategori altyapısı. |
| Sprint kabulü | Restoran menü kategorilerini kolayca yönetebilmelidir. |

### Backend Görevleri

| Kod | Görev | Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S2-BE-01 | Kategori CRUD | GET/POST/PUT/DELETE /menu-categories. | Kategori API | CRUD çalışmalı. |
| S2-BE-02 | Sıralama | PATCH /menu-categories/reorder. | Sort order | Public sıra değişmeli. |
| S2-BE-03 | Aktif/pasif | Pasif kategori publicte görünmez. | Yayın kontrolü | Public endpoint pasifi dönmemeli. |
| S2-BE-04 | Çeviri altyapısı | Kategori translations endpointi. | Çok dil temeli | Dile göre kategori dönmeli. |
| S2-BE-05 | Silme kuralı | Aktif ürünlü kategori silinirken uyarı. | Güvenli silme | Veri kaybı engellenmeli. |

### Frontend Görevleri

| Kod | Görev | Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S2-FE-01 | Kategori listesi | Kart/liste görünümü. | Kategori ekranı | Sıra ve durum görünmeli. |
| S2-FE-02 | Kategori formu | Ad, açıklama, görsel, aktiflik. | Ekle/düzenle | Validation olmalı. |
| S2-FE-03 | Sürükle-bırak | Kategori sıralama. | Reorder UI | Kaydedince public sıra değişmeli. |
| S2-FE-04 | Çeviri formu | TR/EN/DE/RU alanları. | Çok dil UI | Eksik çeviri uyarısı. |
| S2-FE-05 | Silme modalı | Onay ve uyarı. | Güvenli UX | İptal/Onay çalışmalı. |

### Veritabanı Görevleri

| Kod | Tablo/İş | Alanlar/Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S2-DB-01 | menu_categories | restaurant_id, name, slug, description, image_url, sort_order, is_active. | Kategori tablosu | restaurant_id index. |
| S2-DB-02 | menu_category_translations | category_id, language_code, name, description. | Çeviri tablosu | Unique category+lang. |
| S2-DB-03 | Index | restaurant_id, sort_order, is_active. | Performans | Public menü hızlı. |

### Test ve Kabul Senaryoları

| Kod | Senaryo | Adımlar | Beklenen Sonuç | Test Türü |
| --- | --- | --- | --- | --- |
| S2-QA-01 | Kategori ekleme | Yeni kategori eklenir. | Listede görünür | UI+API |
| S2-QA-02 | Sıralama | Kategori sırası değiştirilir. | Public sıra değişir | E2E |
| S2-QA-03 | Pasif kategori | Kategori pasife alınır. | Publicte görünmez | Public test |


## Sprint 3 — Ürün Yönetimi

| Alan | Açıklama |
|---|---|
| Sprint amacı | Menü ürünlerinin fiyat, görsel, stok ve özellikleriyle yönetilmesini sağlamak. |
| Ana çıktı | Ürün yönetimi, fiyat/stok/görsel ve çeviri yapısı. |
| Sprint kabulü | Restoran ürünlerini panelden güncelleyip QR menüde anında gösterebilmelidir. |

### Backend Görevleri

| Kod | Görev | Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S3-BE-01 | Ürün CRUD | GET/POST/PUT/DELETE /menu-items. | Ürün API | CRUD çalışmalı. |
| S3-BE-02 | Görsel yükleme | POST /menu-items/{id}/image. | Ürün görseli | Dosya kaydedilmeli. |
| S3-BE-03 | Stok/aktiflik | availability ve is_active değişimi. | Yayın kontrolü | Tükendi/aktif doğru dönmeli. |
| S3-BE-04 | Fiyat validasyonu | price, discounted_price kuralları. | Fiyat güvenliği | Negatif fiyat engellenmeli. |
| S3-BE-05 | Sıralama | Kategori içi ürün sırası. | Sort order | Public menüye yansımalı. |
| S3-BE-06 | Ürün çevirileri | name/description translations. | Çok dil ürün | Dile göre ürün dönmeli. |

### Frontend Görevleri

| Kod | Görev | Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S3-FE-01 | Ürün listesi | Kategori filtresiyle ürünler. | Ürün ekranı | Filtre çalışmalı. |
| S3-FE-02 | Ürün formu | Ad, açıklama, kategori, fiyat, indirim, görsel. | Ekle/düzenle | Zorunlu alanlar. |
| S3-FE-03 | Etiket switchleri | Vejetaryen, vegan, glutensiz, öne çıkan. | Ürün özellikleri | Publicte görünmeli. |
| S3-FE-04 | Stokta var/yok | Tükendi gösterimi. | Availability UI | Publice yansımalı. |
| S3-FE-05 | Çeviri alanları | TR/EN/DE/RU ürün metinleri. | Çeviri UI | Kaydetme çalışmalı. |

### Veritabanı Görevleri

| Kod | Tablo/İş | Alanlar/Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S3-DB-01 | menu_items | restaurant_id, category_id, name, slug, description, price, discounted_price, currency, image_url, preparation_time_minutes, calories, spice_level, is_vegetarian, is_vegan, is_gluten_free, is_featured, is_available, is_active, sort_order. | Ürün tablosu | category_id index. |
| S3-DB-02 | menu_item_translations | menu_item_id, language_code, name, description. | Çeviri tablosu | Unique item+lang. |
| S3-DB-03 | price constraints | price >= 0, discounted_price <= price opsiyonel. | Veri güvenliği | Hatalı fiyat kaydedilmemeli. |

### Test ve Kabul Senaryoları

| Kod | Senaryo | Adımlar | Beklenen Sonuç | Test Türü |
| --- | --- | --- | --- | --- |
| S3-QA-01 | Ürün ekleme | Yeni ürün eklenir. | Public menüde görünür | E2E |
| S3-QA-02 | Fiyat güncelleme | Ürün fiyatı değiştirilir. | Anında yansır | UI+API |
| S3-QA-03 | Tükendi | is_available=false yapılır. | Publicte tükendi görünür | Public |
| S3-QA-04 | Çeviri | İngilizce ürün adı girilir. | EN menüde görünür | Çok dil |


## Sprint 4 — Public QR Menü Sayfası

| Alan | Açıklama |
|---|---|
| Sprint amacı | Müşterinin QR kodu okutarak menüyü hızlı ve mobil uyumlu görmesini sağlamak. |
| Ana çıktı | Mobil public QR menü, ürün kartları, arama ve detay ekranı. |
| Sprint kabulü | Müşteri QR okuttuğunda menüyü login olmadan hızlıca görüntüleyebilmelidir. |

### Backend Görevleri

| Kod | Görev | Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S4-BE-01 | Public restoran | GET /public/restaurants/{slug}. | Restoran public bilgi | Aktif restoran dönmeli. |
| S4-BE-02 | Public menü | GET /public/restaurants/{slug}/menu. | Kategori+ürün ağacı | Sadece aktif veriler. |
| S4-BE-03 | Arama/filtre | Ürün adı, kategori, etiket. | Public search | Doğru sonuç. |
| S4-BE-04 | Dil parametresi | ?lang=en ile çevirili veri. | Dile göre menü | Fallback çalışmalı. |
| S4-BE-05 | Cache hazırlığı | Public menü hızlı dönmeli. | Performans | Sorgu yavaşlamamalı. |

### Frontend Görevleri

| Kod | Görev | Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S4-FE-01 | QR ana ekran | Logo, restoran adı, kategori tabları. | Public menü | Mobilde hızlı açılmalı. |
| S4-FE-02 | Ürün kartı | Görsel, ad, fiyat, kısa açıklama, tükendi etiketi. | Ürün listeleme | Okunaklı olmalı. |
| S4-FE-03 | Ürün detay modalı | Detay açıklama, alerjen/etiket alanı için hazır yapı. | Detay UI | Modal düzgün çalışmalı. |
| S4-FE-04 | Arama | Müşteri ürünü arar. | Search UI | Sonuç anında değişmeli. |
| S4-FE-05 | WhatsApp/yorum butonu | İletişim ve Google yorum. | CTA butonları | Doğru link açılmalı. |

### Veritabanı Görevleri

| Kod | Tablo/İş | Alanlar/Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S4-DB-01 | Public query view | Aktif kategori/ürünleri hızlı çekmek için opsiyonel view. | Performans view | Sıralama doğru. |
| S4-DB-02 | Public settings dependency | Restoran ayarlarındaki iletişim/public durum kullanılır. | Ayar bağlantısı | Pasif restoran menü açmamalı. |

### Test ve Kabul Senaryoları

| Kod | Senaryo | Adımlar | Beklenen Sonuç | Test Türü |
| --- | --- | --- | --- | --- |
| S4-QA-01 | QR menü açılışı | Public slug URL mobilde açılır. | Menü görünür | Mobil test |
| S4-QA-02 | Pasif ürün/kategori | Pasif veriler kontrol edilir. | Publicte görünmez | Negatif |
| S4-QA-03 | Arama | Ürün adıyla arama yapılır. | Doğru filtrelenir | UI |
| S4-QA-04 | Hız | Menü açılış süresi kontrol edilir. | Kabul edilebilir hız | Performans |


## Sprint 5 — Çok Dil Desteği

| Alan | Açıklama |
|---|---|
| Sprint amacı | Menünün turistik kullanım için TR/EN/DE/RU dillerinde çalışmasını sağlamak. |
| Ana çıktı | Çok dilli public menü ve admin çeviri yönetimi. |
| Sprint kabulü | Turist müşteri seçtiği dilde menüyü görebilmelidir. |

### Backend Görevleri

| Kod | Görev | Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S5-BE-01 | Dil listesi | GET /languages. | Dil datası | TR/EN/DE/RU dönmeli. |
| S5-BE-02 | Restoran dilleri | PUT /restaurants/languages. | Aktif dil ayarı | Pasif dil görünmemeli. |
| S5-BE-03 | Kategori/ürün çeviri | Translation endpointleri. | Çok dilli veri | Dile göre dönmeli. |
| S5-BE-04 | Fallback | Eksik çeviri varsayılan dile düşer. | Boş metin önleme | Varsayılan dil görünmeli. |

### Frontend Görevleri

| Kod | Görev | Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S5-FE-01 | Public dil seçici | Menüde dil değiştirme. | Dil UI | Seçim korunmalı. |
| S5-FE-02 | Admin çeviri formları | Kategori ve ürün çeviri alanları. | Çeviri yönetimi | Kaydetme çalışmalı. |
| S5-FE-03 | Eksik çeviri uyarısı | Admin panelde gösterilir. | Uyarı | Eksik dil anlaşılır olmalı. |

### Veritabanı Görevleri

| Kod | Tablo/İş | Alanlar/Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S5-DB-01 | languages | code, name, is_active. | Dil tablosu | Seed diller. |
| S5-DB-02 | restaurant_languages | restaurant_id, language_code, is_enabled. | Restoran dil ayarı | Unique restaurant+lang. |
| S5-DB-03 | translation tabloları | menu_category_translations, menu_item_translations genişletilir. | Çeviri altyapısı | Fallback desteklemeli. |

### Test ve Kabul Senaryoları

| Kod | Senaryo | Adımlar | Beklenen Sonuç | Test Türü |
| --- | --- | --- | --- | --- |
| S5-QA-01 | Dil değişimi | TR/EN/DE/RU denenir. | İçerik değişir | Public |
| S5-QA-02 | Eksik çeviri | Ürünün Almanca açıklaması boş. | TR açıklama gelir | Fallback |
| S5-QA-03 | Pasif dil | Rusça kapatılır. | Dil seçicide görünmez | Ayar |


## Sprint 6 — QR Kod Üretimi ve Masa Bazlı QR

| Alan | Açıklama |
|---|---|
| Sprint amacı | Restoranın genel veya masa bazlı QR kodlarını indirip yazdırmasını sağlamak. |
| Ana çıktı | Genel ve masa bazlı QR kod yönetimi, PNG/PDF çıktı. |
| Sprint kabulü | Restoran masalara koyacağı QR kodları sistemden alabilmelidir. |

### Backend Görevleri

| Kod | Görev | Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S6-BE-01 | QR üretimi | Genel restoran QR ve masa QR token üretimi. | QR servisi | Token benzersiz olmalı. |
| S6-BE-02 | Masa CRUD | GET/POST/PUT/DELETE /tables. | Masa yönetimi | Masa no unique olmalı. |
| S6-BE-03 | PNG indirme | QR PNG üretimi. | QR görseli | Dosya açılmalı. |
| S6-BE-04 | PDF çıktı | Toplu masa QR PDF. | Yazdırılabilir PDF | Tüm masalar olmalı. |
| S6-BE-05 | QR pasifleştirme | Token revoke/pasif. | Güvenlik | Eski QR çalışmamalı. |

### Frontend Görevleri

| Kod | Görev | Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S6-FE-01 | QR yönetim ekranı | Genel ve masa QR listesi. | QR paneli | Önizleme olmalı. |
| S6-FE-02 | Masa yönetimi | Masa ekle/düzenle/sil. | Masa UI | Validation çalışmalı. |
| S6-FE-03 | QR önizleme/indirme | PNG ve PDF indir. | İndirme UI | Dosyalar inmeli. |
| S6-FE-04 | QR kart tasarımı | Logo, masa no, QR, kısa açıklama. | Baskı şablonu | A4 uyumlu olmalı. |

### Veritabanı Görevleri

| Kod | Tablo/İş | Alanlar/Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S6-DB-01 | restaurant_tables | restaurant_id, table_number, table_name, qr_token, is_active. | Masa tablosu | restaurant+table unique. |
| S6-DB-02 | qr_codes | restaurant_id, table_id, type, token, target_url, is_active, revoked_at. | QR tablosu | Tek aktif token kuralı. |

### Test ve Kabul Senaryoları

| Kod | Senaryo | Adımlar | Beklenen Sonuç | Test Türü |
| --- | --- | --- | --- | --- |
| S6-QA-01 | Masa QR | Masa 5 QR okutulur. | URLde masa bilgisi gelir | Manuel |
| S6-QA-02 | PDF çıktı | Toplu QR PDF indirilir. | Tüm masalar var | Doküman |
| S6-QA-03 | Pasif QR | QR pasif edilir, okutulur. | Hata/menü kapalı | Güvenlik |


## Sprint 7 — Tema, Marka ve Görünüm Ayarları

| Alan | Açıklama |
|---|---|
| Sprint amacı | Her restoranın menüsünü kendi marka görünümüne uyarlamasını sağlamak. |
| Ana çıktı | Tema ve marka özelleştirme sistemi. |
| Sprint kabulü | Her restoran kendi logosu ve renkleriyle menüyü yayınlayabilmelidir. |

### Backend Görevleri

| Kod | Görev | Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S7-BE-01 | Tema ayarları | GET/PUT /theme. | Tema API | Ayarlar kaydedilmeli. |
| S7-BE-02 | Logo/kapak görseli | Dosya yükleme. | Branding dosyaları | Publicte görünmeli. |
| S7-BE-03 | Buton görünürlük | WhatsApp ve Google yorum aktif/pasif. | CTA ayarı | Publice yansımalı. |
| S7-BE-04 | Menü layout | Kart/list görünüm seçeneği. | Layout ayarı | Public render değişmeli. |

### Frontend Görevleri

| Kod | Görev | Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S7-FE-01 | Tema ekranı | Renk, logo, kapak, font, layout. | Tema UI | Önizleme olmalı. |
| S7-FE-02 | Renk seçici | Ana/ikincil renk. | Color picker | Publice yansımalı. |
| S7-FE-03 | Önizleme | Public menü örnek görünümü. | Preview | Gerçek menüye yakın. |
| S7-FE-04 | Buton ayarları | WhatsApp/yorum göster-gizle. | CTA ayarı | Publicte değişmeli. |

### Veritabanı Görevleri

| Kod | Tablo/İş | Alanlar/Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S7-DB-01 | restaurant_theme_settings | restaurant_id, logo_url, cover_image_url, primary_color, secondary_color, font_family, menu_layout, show_whatsapp_button, show_google_review_button. | Tema tablosu | Restoran bazlı olmalı. |

### Test ve Kabul Senaryoları

| Kod | Senaryo | Adımlar | Beklenen Sonuç | Test Türü |
| --- | --- | --- | --- | --- |
| S7-QA-01 | Logo/renk | Logo ve renk değişir. | Public menü değişir | UI |
| S7-QA-02 | Buton kapatma | WhatsApp kapatılır. | Publicte görünmez | Ayar |
| S7-QA-03 | Layout değişimi | Kart/list seçilir. | Görünüm değişir | UI |


## Sprint 8 — Kampanya ve Öne Çıkan Ürünler

| Alan | Açıklama |
|---|---|
| Sprint amacı | Restoranın günün ürünü, indirim ve kampanya duyurmasını sağlamak. |
| Ana çıktı | Kampanya, günün ürünü ve öne çıkanlar. |
| Sprint kabulü | Restoran güncel kampanyalarını QR menüde gösterebilmelidir. |

### Backend Görevleri

| Kod | Görev | Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S8-BE-01 | Kampanya CRUD | /promotions endpointleri. | Kampanya API | CRUD çalışmalı. |
| S8-BE-02 | Tarih aralığı | start_date/end_date. | Süre kuralı | Süresi dolan görünmemeli. |
| S8-BE-03 | Ürün bağlantısı | Kampanya ürünle ilişkilendirilebilir. | İlişki | Ürün kartında görünebilir. |
| S8-BE-04 | Çeviri | Kampanya çok dilli olabilir. | Çok dil | Dile göre dönmeli. |

### Frontend Görevleri

| Kod | Görev | Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S8-FE-01 | Kampanya listesi | Aktif/pasif/süre bilgisi. | Liste | Filtrelenebilir olmalı. |
| S8-FE-02 | Kampanya formu | Başlık, açıklama, tip, ürün, tarih. | Form | Validation olmalı. |
| S8-FE-03 | Public banner | Öne çıkan kampanya alanı. | Banner UI | Sadece aktif kampanya. |
| S8-FE-04 | Günün ürünü | Menü üstünde öne çıkarılır. | Featured UI | Ürün detayına bağlanmalı. |

### Veritabanı Görevleri

| Kod | Tablo/İş | Alanlar/Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S8-DB-01 | promotions | restaurant_id, menu_item_id, title, description, promotion_type, start_date, end_date, is_active. | Kampanya tablosu | Tarih index. |
| S8-DB-02 | promotion_translations | promotion_id, language_code, title, description. | Çeviri tablosu | Unique promo+lang. |

### Test ve Kabul Senaryoları

| Kod | Senaryo | Adımlar | Beklenen Sonuç | Test Türü |
| --- | --- | --- | --- | --- |
| S8-QA-01 | Aktif kampanya | Bugün aktif kampanya eklenir. | Publicte görünür | E2E |
| S8-QA-02 | Süresi dolan | Bitiş tarihi geçmiş kampanya. | Publicte görünmez | İş kuralı |
| S8-QA-03 | Çeviri | İngilizce kampanya girilir. | EN menüde görünür | Çok dil |


## Sprint 9 — Alerjen ve Ürün Etiketleri

| Alan | Açıklama |
|---|---|
| Sprint amacı | Ürünlerde alerjen, diyet ve özel etiket bilgilerini göstermek. |
| Ana çıktı | Alerjen ve ürün etiketi yönetimi. |
| Sprint kabulü | Müşteri ürün detayında alerjen ve özel ürün bilgisini görebilmelidir. |

### Backend Görevleri

| Kod | Görev | Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S9-BE-01 | Alerjen listesi | GET /allergens. | Alerjen datası | Varsayılanlar dönmeli. |
| S9-BE-02 | Ürüne alerjen ekleme | POST/DELETE item allergens. | İlişki API | Ekle/sil çalışmalı. |
| S9-BE-03 | Etiket CRUD | Restoran özel etiketleri. | Tag API | Renk/ad yönetilmeli. |
| S9-BE-04 | Public gösterim | Ürün detayında alerjen/etiketler döner. | Public veri | Doğru ürünle dönmeli. |

### Frontend Görevleri

| Kod | Görev | Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S9-FE-01 | Ürün formu alerjen | Çoklu seçim. | Alerjen UI | Kaydetme çalışmalı. |
| S9-FE-02 | Etiket yönetimi | Vegan, popüler, yeni vb. | Tag UI | Ürüne eklenebilmeli. |
| S9-FE-03 | Public detay | Alerjen uyarısı ve etiketler. | Ürün detay UI | Anlaşılır görünmeli. |

### Veritabanı Görevleri

| Kod | Tablo/İş | Alanlar/Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S9-DB-01 | allergens | code, name. | Alerjen tablosu | Seed edilmeli. |
| S9-DB-02 | menu_item_allergens | menu_item_id, allergen_id. | Çok-çok ilişki | Duplicate engellenmeli. |
| S9-DB-03 | tags | restaurant_id, name, color, is_active. | Etiket tablosu | Restoran bazlı. |
| S9-DB-04 | menu_item_tags | menu_item_id, tag_id. | Ürün-etiket | Duplicate engellenmeli. |

### Test ve Kabul Senaryoları

| Kod | Senaryo | Adımlar | Beklenen Sonuç | Test Türü |
| --- | --- | --- | --- | --- |
| S9-QA-01 | Alerjen ekleme | Ürüne gluten eklenir. | Publicte görünür | E2E |
| S9-QA-02 | Etiket ekleme | Popüler etiketi eklenir. | Ürün kartında görünür | UI |
| S9-QA-03 | Silme | Alerjen kaldırılır. | Publicten kalkar | Entegrasyon |


## Sprint 10 — Basit İstatistik ve Ziyaret Takibi

| Alan | Açıklama |
|---|---|
| Sprint amacı | Restoran sahibine menü görüntülenme ve popüler ürün bilgisi vermek. |
| Ana çıktı | Anonim menü görüntülenme ve popüler ürün istatistikleri. |
| Sprint kabulü | Restoran sahibi hangi ürünlerin daha çok incelendiğini görebilmelidir. |

### Backend Görevleri

| Kod | Görev | Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S10-BE-01 | Menu view event | POST /public/analytics/menu-view. | Menü görüntüleme kaydı | Anonim kayıt oluşmalı. |
| S10-BE-02 | Item view event | POST /public/analytics/item-view. | Ürün detay kaydı | Ürün view artmalı. |
| S10-BE-03 | Analytics summary | GET /analytics/summary. | Özet istatistik | Tarih filtresi çalışmalı. |
| S10-BE-04 | Top items | GET /analytics/top-items. | Popüler ürünler | Sıralama doğru olmalı. |
| S10-BE-05 | Gizlilik | IP tam saklanmaz, kişisel veri toplanmaz. | KVKK dostu yapı | Anonim olmalı. |

### Frontend Görevleri

| Kod | Görev | Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S10-FE-01 | Public event gönderimi | Menü açılınca ve ürün detayı açılınca event. | Event entegrasyonu | Fazla tekrar engellenmeli. |
| S10-FE-02 | Analytics dashboard | Ziyaret, popüler ürün, dil kullanım. | İstatistik ekranı | Grafikler doğru. |
| S10-FE-03 | Tarih filtresi | Bugün/hafta/ay. | Filtre UI | Doğru aralık. |

### Veritabanı Görevleri

| Kod | Tablo/İş | Alanlar/Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S10-DB-01 | menu_view_events | restaurant_id, table_id, language_code, user_agent, created_at. | Menü event | Kişisel veri minimum. |
| S10-DB-02 | menu_item_view_events | restaurant_id, menu_item_id, table_id, language_code, created_at. | Ürün event | Indexli olmalı. |
| S10-DB-03 | Analytics index | restaurant_id, created_at, menu_item_id. | Performans | Rapor hızlı olmalı. |

### Test ve Kabul Senaryoları

| Kod | Senaryo | Adımlar | Beklenen Sonuç | Test Türü |
| --- | --- | --- | --- | --- |
| S10-QA-01 | Menü view | Public menü açılır. | Event oluşur | Entegrasyon |
| S10-QA-02 | Ürün view | Ürün detayı açılır. | Item event oluşur | Entegrasyon |
| S10-QA-03 | Dashboard doğruluğu | Event sayısı elle karşılaştırılır. | Sayılar doğru | Veri testi |


## Sprint 11 — Toplu Fiyat Güncelleme ve Excel İşlemleri

| Alan | Açıklama |
|---|---|
| Sprint amacı | Restoranın çok sayıda ürünü hızlı güncellemesini sağlamak. |
| Ana çıktı | Toplu fiyat güncelleme, import/export ve fiyat logları. |
| Sprint kabulü | Restoran çok ürünlü menüyü tek tek uğraşmadan güncelleyebilmelidir. |

### Backend Görevleri

| Kod | Görev | Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S11-BE-01 | Toplu fiyat update | PATCH /menu-items/bulk-price-update. | Yüzde/tutar zam | Doğru uygulanmalı. |
| S11-BE-02 | Toplu aktiflik/stok | Seçili ürünleri pasif/tükendi yapma. | Bulk action | Seçilenler değişmeli. |
| S11-BE-03 | Export | GET /menu-items/export. | CSV/Excel dışa aktarım | Kolonlar doğru. |
| S11-BE-04 | Import | POST /menu-items/import. | CSV/Excel içe aktarım | Hatalı satırlar raporlanmalı. |
| S11-BE-05 | Fiyat logu | Eski/yeni fiyat kayıt altına alınır. | Audit | Her fiyat değişimi loglanmalı. |

### Frontend Görevleri

| Kod | Görev | Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S11-FE-01 | Toplu işlem toolbar | Ürün seçimi sonrası aksiyonlar. | Bulk UI | Seçim çalışmalı. |
| S11-FE-02 | Zam modalı | Kategori/ürün bazlı yüzde zam. | Fiyat UI | Önizleme olmalı. |
| S11-FE-03 | Import ekranı | Dosya yükleme ve hata satırları. | Import UI | Hatalar gösterilmeli. |
| S11-FE-04 | Export butonu | Ürünleri dışa aktar. | Export UI | Dosya inmeli. |

### Veritabanı Görevleri

| Kod | Tablo/İş | Alanlar/Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S11-DB-01 | price_change_logs | restaurant_id, menu_item_id, old_price, new_price, changed_by_user_id, reason, created_at. | Fiyat geçmişi | Her değişim kayıt. |
| S11-DB-02 | import_jobs | restaurant_id, file_url, status, total_rows, success_rows, failed_rows, error_report_url. | Import takip | Hata raporu tutulmalı. |

### Test ve Kabul Senaryoları

| Kod | Senaryo | Adımlar | Beklenen Sonuç | Test Türü |
| --- | --- | --- | --- | --- |
| S11-QA-01 | Kategori zammı | İçecek kategorisine %10 zam. | Fiyatlar doğru | İş kuralı |
| S11-QA-02 | Import hatası | Eksik fiyatlı satır yüklenir. | Hata raporu oluşur | Negatif |
| S11-QA-03 | Export | Ürünler export edilir. | Dosya doğru | Doküman |


## Sprint 12 — Yayınlama, Demo ve Satışa Hazırlık

| Alan | Açıklama |
|---|---|
| Sprint amacı | QR menü ürününü gerçek restorana satılabilir hale getirmek. |
| Ana çıktı | Canlıya hazır QR menü MVP, demo restoran, QR baskı dosyaları. |
| Sprint kabulü | Bir restoran ürünü gerçek müşterilerine QR menü olarak sunabilmelidir. |

### Backend Görevleri

| Kod | Görev | Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S12-BE-01 | Production ortam | API canlıya alınır. | Canlı backend | Health çalışmalı. |
| S12-BE-02 | Migration/backup | Canlı DB ve yedekleme. | Canlı DB | Backup denenmeli. |
| S12-BE-03 | Cache/rate limit | Public menü performans ve spam koruması. | Performans/güvenlik | Menü hızlı açılmalı. |
| S12-BE-04 | Loglama | Hata ve kritik işlem logları. | Monitoring | Hata takip edilebilir. |

### Frontend Görevleri

| Kod | Görev | Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S12-FE-01 | Production build | Admin ve public yayına alınır. | Canlı frontend | Sayfalar açılmalı. |
| S12-FE-02 | Mobil son kontrol | QR menü gerçek cihazlarda test. | Mobil UX | Okunabilir olmalı. |
| S12-FE-03 | Demo restoran | Örnek kategori/ürün/dil/tema. | Demo hesap | Satış demosu hazır. |
| S12-FE-04 | QR çıktı tasarımı | Restoran için basılabilir kart. | Satış materyali | PDF düzgün olmalı. |
| S12-FE-05 | Kullanıcı dokümanı | Menü nasıl güncellenir kısa rehber. | Doküman | Müşteri anlayabilmeli. |

### Veritabanı Görevleri

| Kod | Tablo/İş | Alanlar/Detay | Beklenen Çıktı | Test/Kontrol |
| --- | --- | --- | --- | --- |
| S12-DB-01 | Seed data | Demo restoran, kategoriler, ürünler, diller, kampanyalar, masalar. | Demo veri | Tüm akış çalışmalı. |
| S12-DB-02 | Index final | restaurant_id, category_id, is_active, sort_order indexleri. | Performans | Public menü hızlı. |

### Test ve Kabul Senaryoları

| Kod | Senaryo | Adımlar | Beklenen Sonuç | Test Türü |
| --- | --- | --- | --- | --- |
| S12-QA-01 | QR E2E | Masa QR okutulur, menü açılır, ürün detay bakılır. | Akış tamamlanır | E2E |
| S12-QA-02 | Fiyat güncelleme | Admin fiyat değiştirir. | Publicte anında görünür | Entegrasyon |
| S12-QA-03 | Çok dil | EN/DE/RU menü kontrol edilir. | İçerik doğru | Çok dil |
| S12-QA-04 | Satış demo | Demo restoran müşteriye gösterilir. | Anlaşılır demo | Kabul |

## Temel Veritabanı Tablo Özeti

| Tablo | Amaç | Kritik Alanlar |
|---|---|---|
| restaurants | Restoran ana bilgisi | name, slug, phone, whatsapp_phone, logo, currency, is_active |
| users | Panel kullanıcıları | restaurant_id, full_name, email, password_hash |
| roles/user_roles | Yetki sistemi | role code ve user ilişkisi |
| menu_categories | Menü kategorileri | restaurant_id, name, slug, sort_order, is_active |
| menu_category_translations | Kategori çevirileri | category_id, language_code, name, description |
| menu_items | Ürünler | category_id, name, price, image_url, is_available, is_active, sort_order |
| menu_item_translations | Ürün çevirileri | menu_item_id, language_code, name, description |
| languages | Desteklenen diller | code, name, is_active |
| restaurant_languages | Restoran aktif dilleri | restaurant_id, language_code, is_enabled |
| restaurant_tables | Masa bilgileri | table_number, qr_token, is_active |
| qr_codes | QR kayıtları | token, target_url, type, is_active |
| restaurant_theme_settings | Tema/marka | logo, cover, primary_color, menu_layout |
| promotions | Kampanyalar | title, promotion_type, start_date, end_date, is_active |
| promotion_translations | Kampanya çevirileri | promotion_id, language_code, title, description |
| allergens | Alerjenler | code, name |
| menu_item_allergens | Ürün-alerjen ilişkisi | menu_item_id, allergen_id |
| tags | Restoran etiketleri | name, color, is_active |
| menu_item_tags | Ürün-etiket ilişkisi | menu_item_id, tag_id |
| menu_view_events | Menü görüntülenmeleri | restaurant_id, table_id, language_code, created_at |
| menu_item_view_events | Ürün görüntülenmeleri | restaurant_id, menu_item_id, language_code, created_at |
| price_change_logs | Fiyat geçmişi | old_price, new_price, changed_by_user_id |
| import_jobs | Excel import takip | status, total_rows, success_rows, failed_rows |

## MVP Teslim Sırası Önerisi

| Sıra | Sprint | Neden Öncelikli? |
|---:|---|---|
| 1 | Sprint 0 | Teknik temel kurulur. |
| 2 | Sprint 1 | Restoran hesabı ve veri izolasyonu sağlanır. |
| 3 | Sprint 2 | Menü kategorileri oluşur. |
| 4 | Sprint 3 | Ürün yönetimi eklenir. |
| 5 | Sprint 4 | QR menünün müşteriye görünen hali çıkar. |
| 6 | Sprint 5 | Çok dil desteği turistik satış değerini artırır. |
| 7 | Sprint 6 | QR çıktı olmadan fiziksel kullanım olmaz. |
| 8 | Sprint 7 | Tema/marka ürünü farklı işletmelere satılabilir yapar. |
| 9 | Sprint 8/9/10 | Kampanya, alerjen ve istatistik satış değerini yükseltir. |
| 10 | Sprint 11 | Toplu işlem çok ürünlü restoranlarda kolaylık sağlar. |

## Pilot Kullanım Senaryosu

| Adım | Senaryo | Beklenen Durum |
|---:|---|---|
| 1 | Restoran sahibi login olur. | Admin panel açılır. |
| 2 | Restoran bilgileri ve logo girilir. | Public menü markalı görünür. |
| 3 | Kategoriler oluşturulur. | Menüde sırayla görünür. |
| 4 | Ürünler ve fiyatlar eklenir. | Public menüde listelenir. |
| 5 | İngilizce/Almanca çeviriler girilir. | Dil değişince içerik değişir. |
| 6 | Masa QR kodları oluşturulur. | PDF çıktı alınır. |
| 7 | Müşteri QR okutur. | Menü mobilde açılır. |
| 8 | Ürün detayı açılır. | Detay ve etiketler görünür. |
| 9 | Restoran fiyatı günceller. | Public menüde yeni fiyat görünür. |
| 10 | İstatistik ekranı açılır. | Menü/ürün görüntülenmeleri görünür. |
