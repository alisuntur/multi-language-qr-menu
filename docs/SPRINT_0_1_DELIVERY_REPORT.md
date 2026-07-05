# Sprint 0-1 Delivery Report

## Kapsam

Bu teslimde `Sprint 0` ve `Sprint 1` için temel kurulum, auth altyapısı ve restoran hesabı yönetimi tamamlandı.

## Tamamlanan İşler

### Sprint 0
- `ASP.NET Core Web API` proje iskeleti kuruldu.
- `Swagger UI` eklendi.
- Global hata yakalama ve standart API response yapısı kuruldu.
- `GET /health` endpointi eklendi.
- `React + Vite + TypeScript` frontend iskeleti kuruldu.
- Admin layout ve public mobil layout iskeleti oluşturuldu.
- API istemci mantığı frontend içinde kuruldu.
- `PostgreSQL` için temel tablo scripti yazıldı.

### Sprint 1
- `login`, `logout`, `refresh`, `me` auth uçları eklendi.
- `JWT + refresh token` akışı kuruldu.
- Restoran bazlı veri izolasyonu için `restaurant_id` claim yapısı eklendi.
- `GET/PUT /api/restaurants/current` uçları eklendi.
- Rol sabitleri ve rol bazlı menü görünürlüğü eklendi.
- `RESTAURANT_OWNER` ve `BRANCH_MANAGER` için restoran ayarlarını güncelleme yetkisi tanımlandı.
- Varsayılan demo restoran ve restoran sahibi seed edildi.
- Audit log kayıtları login, logout ve restoran güncelleme akışlarına bağlandı.

## Veritabanı

### Oluşturulan Tablolar
- `restaurants`
- `users`
- `roles`
- `user_roles`
- `refresh_tokens`
- `audit_logs`

### Local Veritabanı
- Veritabanı adı: `multi_language_qr_menu`
- Kullanıcı: `postgres`

## Demo Giriş Bilgileri
- E-posta: `owner@demoqrmenu.local`
- Şifre: `ChangeMe123!`

## Doğrulama Sonuçları

### Backend
- `dotnet restore` başarılı
- `dotnet build` başarılı
- `GET /health` başarılı
- `POST /api/auth/login` başarılı
- `GET /api/auth/me` başarılı
- `GET /api/restaurants/current` başarılı
- `PUT /api/restaurants/current` başarılı

### Frontend
- `npm install` başarılı
- `npm run build` başarılı`r`n- Frontend login akışında görülen `Failed to fetch` problemi CORS güncellemesiyle giderildi

## Notlar
- Frontend bağımlılıklarında `npm audit` çıktısında `1 moderate` ve `1 high` seviye uyarı görüldü.
- Backend, farklı çalışma dizinlerinden başlatıldığında da config ve database scriptini bulabilmesi için güçlendirildi.
- Henüz commit atılmadı.
- Sen onay verdikten sonra commit ve istersen push adımına geçeceğim.

