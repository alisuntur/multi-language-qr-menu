import {
  FormEvent,
  createContext,
  useContext,
  useDeferredValue,
  useEffect,
  useMemo,
  useState,
  type CSSProperties,
  type PropsWithChildren
} from "react";
import { Navigate, NavLink, Outlet, Route, Routes, useLocation, useNavigate, useParams } from "react-router-dom";

type ApiEnvelope<T> = {
  success: boolean;
  message: string;
  data: T;
  errors: string[];
};

type Translation = {
  languageCode: string;
  name: string;
  description: string;
};

type AuthUser = {
  id: string;
  fullName: string;
  email: string;
  phone: string;
  isActive: boolean;
  restaurantId: string;
  restaurantName: string;
  restaurantSlug: string;
  restaurantIsActive: boolean;
  roles: string[];
};

type AuthSession = {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: AuthUser;
};

type RestaurantSettings = {
  id: string;
  name: string;
  slug: string;
  phone: string;
  whatsappPhone: string;
  email: string;
  address: string;
  logoUrl: string;
  defaultLanguage: string;
  currency: string;
  isActive: boolean;
};

type HealthPayload = {
  status: string;
  serverTimeUtc: string;
};

type Category = {
  id: string;
  restaurantId: string;
  name: string;
  slug: string;
  description: string;
  imageUrl: string;
  sortOrder: number;
  isActive: boolean;
  itemCount: number;
  translations: Translation[];
};

type MenuItem = {
  id: string;
  restaurantId: string;
  categoryId: string;
  categoryName: string;
  name: string;
  slug: string;
  description: string;
  price: number;
  discountedPrice: number | null;
  currency: string;
  imageUrl: string;
  preparationTimeMinutes: number | null;
  calories: number | null;
  spiceLevel: number;
  isVegetarian: boolean;
  isVegan: boolean;
  isGlutenFree: boolean;
  isFeatured: boolean;
  isAvailable: boolean;
  isActive: boolean;
  sortOrder: number;
  translations: Translation[];
};

type CategoryForm = {
  id?: string;
  name: string;
  slug: string;
  description: string;
  imageUrl: string;
  isActive: boolean;
  translations: Translation[];
};

type MenuItemForm = {
  id?: string;
  categoryId: string;
  name: string;
  slug: string;
  description: string;
  price: string;
  discountedPrice: string;
  currency: string;
  imageUrl: string;
  preparationTimeMinutes: string;
  calories: string;
  spiceLevel: string;
  isVegetarian: boolean;
  isVegan: boolean;
  isGlutenFree: boolean;
  isFeatured: boolean;
  isAvailable: boolean;
  isActive: boolean;
  translations: Translation[];
};


type LanguageOption = {
  code: string;
  name: string;
  isActive: boolean;
  isEnabled: boolean;
  isDefault: boolean;
};

type PublicRestaurant = {
  id: string;
  name: string;
  slug: string;
  phone: string;
  whatsappPhone: string;
  email: string;
  address: string;
  logoUrl: string;
  defaultLanguage: string;
  selectedLanguage: string;
  currency: string;
  activeLanguages: LanguageOption[];
};

type PublicMenuItem = {
  id: string;
  slug: string;
  name: string;
  description: string;
  imageUrl: string;
  price: number;
  discountedPrice: number | null;
  currency: string;
  preparationTimeMinutes: number | null;
  calories: number | null;
  spiceLevel: number;
  isVegetarian: boolean;
  isVegan: boolean;
  isGlutenFree: boolean;
  isFeatured: boolean;
  isAvailable: boolean;
  sortOrder: number;
};

type PublicMenuCategory = {
  id: string;
  slug: string;
  name: string;
  description: string;
  imageUrl: string;
  sortOrder: number;
  items: PublicMenuItem[];
};

type PublicMenuPayload = {
  restaurant: PublicRestaurant;
  languageCode: string;
  search: string;
  categories: PublicMenuCategory[];
};
type RestaurantTable = {
  id: string;
  tableNumber: number;
  tableName: string;
  qrToken: string;
  isActive: boolean;
  targetUrl: string;
  qrImageUrl: string;
};

type QrCodeCard = {
  id: string;
  type: string;
  label: string;
  token: string;
  isActive: boolean;
  targetUrl: string;
  qrImageUrl: string;
};

type ThemeSettings = {
  restaurantId: string;
  logoUrl: string;
  coverImageUrl: string;
  primaryColor: string;
  secondaryColor: string;
  fontFamily: string;
  menuLayout: "cards" | "list";
  showWhatsappButton: boolean;
  showGoogleReviewButton: boolean;
  googleReviewUrl: string;
};

type AuthContextValue = {
  session: AuthSession | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  refreshProfile: () => Promise<void>;
};

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://127.0.0.1:5099";
const STORAGE_KEY = "mlqm-session";
const LANGUAGE_OPTIONS = ["tr", "en", "de", "ru"] as const;
const AuthContext = createContext<AuthContextValue | null>(null);

async function request<T>(path: string, init?: RequestInit, token?: string): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(init?.headers ?? {})
    }
  });

  const payload = (await response.json()) as ApiEnvelope<T>;
  if (!response.ok || !payload.success) {
    throw new Error(payload.errors?.[0] || payload.message || "Request failed.");
  }

  return payload.data;
}

function ensureTranslations(translations: Translation[]) {
  return LANGUAGE_OPTIONS.map((languageCode) => {
    const existing = translations.find((translation) => translation.languageCode === languageCode);
    return existing ?? { languageCode, name: "", description: "" };
  });
}

function createEmptyCategoryForm(): CategoryForm {
  return {
    name: "",
    slug: "",
    description: "",
    imageUrl: "",
    isActive: true,
    translations: ensureTranslations([])
  };
}

function createEmptyItemForm(categoryId = ""): MenuItemForm {
  return {
    categoryId,
    name: "",
    slug: "",
    description: "",
    price: "",
    discountedPrice: "",
    currency: "TRY",
    imageUrl: "",
    preparationTimeMinutes: "",
    calories: "",
    spiceLevel: "0",
    isVegetarian: false,
    isVegan: false,
    isGlutenFree: false,
    isFeatured: false,
    isAvailable: true,
    isActive: true,
    translations: ensureTranslations([])
  };
}

function mapCategoryToForm(category: Category): CategoryForm {
  return {
    id: category.id,
    name: category.name,
    slug: category.slug,
    description: category.description,
    imageUrl: category.imageUrl,
    isActive: category.isActive,
    translations: ensureTranslations(category.translations)
  };
}

function mapItemToForm(item: MenuItem): MenuItemForm {
  return {
    id: item.id,
    categoryId: item.categoryId,
    name: item.name,
    slug: item.slug,
    description: item.description,
    price: String(item.price),
    discountedPrice: item.discountedPrice == null ? "" : String(item.discountedPrice),
    currency: item.currency,
    imageUrl: item.imageUrl,
    preparationTimeMinutes: item.preparationTimeMinutes == null ? "" : String(item.preparationTimeMinutes),
    calories: item.calories == null ? "" : String(item.calories),
    spiceLevel: String(item.spiceLevel),
    isVegetarian: item.isVegetarian,
    isVegan: item.isVegan,
    isGlutenFree: item.isGlutenFree,
    isFeatured: item.isFeatured,
    isAvailable: item.isAvailable,
    isActive: item.isActive,
    translations: ensureTranslations(item.translations)
  };
}


function formatMoney(value: number, currency: string) {
  return new Intl.NumberFormat("tr-TR", {
    style: "currency",
    currency,
    maximumFractionDigits: 2
  }).format(value);
}

function normalizePhoneLink(value: string) {
  return value.replace(/[^\d]/g, "");
}

function getMissingTranslationCodes(translations: Translation[]) {
  return translations.filter((translation) => !translation.name.trim()).map((translation) => translation.languageCode.toUpperCase());
}
function createEmptyTableForm() {
  return { id: "", tableNumber: "", tableName: "", isActive: true };
}

function createDefaultTheme(): ThemeSettings {
  return {
    restaurantId: "",
    logoUrl: "",
    coverImageUrl: "",
    primaryColor: "#ff6b35",
    secondaryColor: "#132238",
    fontFamily: "Manrope",
    menuLayout: "cards",
    showWhatsappButton: true,
    showGoogleReviewButton: false,
    googleReviewUrl: ""
  };
}

function buildPrintableHtml(cards: QrCodeCard[], restaurantName: string) {
  const items = cards.map((card) => [
    '<article class="print-card">',
    '<img src="' + card.qrImageUrl + '" alt="' + card.label + '" />',
    '<h2>' + card.label + '</h2>',
    '<p>' + restaurantName + '</p>',
    '<small>' + card.targetUrl + '</small>',
    '</article>'
  ].join('')).join('');

  return [
    '<!doctype html>',
    '<html lang="tr">',
    '<head>',
    '<meta charset="utf-8" />',
    '<title>' + restaurantName + ' QR Çıktıları</title>',
    '<style>body { font-family: Arial, sans-serif; padding: 24px; } h1 { margin: 0 0 20px; } .grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 16px; } .print-card { border: 1px solid #ddd; border-radius: 16px; padding: 16px; text-align: center; page-break-inside: avoid; } .print-card img { width: 220px; height: 220px; object-fit: contain; } .print-card h2 { margin: 12px 0 8px; font-size: 20px; } .print-card p { margin: 0 0 8px; color: #444; } .print-card small { color: #777; word-break: break-all; }</style>',
    '</head>',
    '<body>',
    '<h1>' + restaurantName + ' QR Çıktıları</h1>',
    '<section class="grid">' + items + '</section>',
    '</body>',
    '</html>'
  ].join('');
}

function AuthProvider({ children }: PropsWithChildren) {
  const [session, setSession] = useState<AuthSession | null>(() => {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as AuthSession) : null;
  });
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (session) {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    } else {
      localStorage.removeItem(STORAGE_KEY);
    }
  }, [session]);

  const value = useMemo<AuthContextValue>(() => ({
    session,
    loading,
    async login(email, password) {
      setLoading(true);
      try {
        const nextSession = await request<AuthSession>("/api/auth/login", {
          method: "POST",
          body: JSON.stringify({ email, password })
        });
        setSession(nextSession);
      } finally {
        setLoading(false);
      }
    },
    async logout() {
      if (session) {
        try {
          await request("/api/auth/logout", {
            method: "POST",
            body: JSON.stringify({ refreshToken: session.refreshToken })
          }, session.accessToken);
        } catch {
        }
      }
      setSession(null);
    },
    async refreshProfile() {
      if (!session) return;
      const user = await request<AuthUser>("/api/auth/me", { method: "GET" }, session.accessToken);
      setSession({ ...session, user });
    }
  }), [loading, session]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error("Auth context is not available.");
  return context;
}

function ProtectedRoute() {
  const { session } = useAuth();
  const location = useLocation();
  if (!session) return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  return <Outlet />;
}

function LoginPage() {
  const { login, loading } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = useState("owner@demoqrmenu.local");
  const [password, setPassword] = useState("ChangeMe123!");
  const [error, setError] = useState("");

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");
    try {
      await login(email, password);
      const target = (location.state as { from?: string } | null)?.from ?? "/admin";
      navigate(target, { replace: true });
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Giriş başarısız oldu.");
    }
  }

  return (
    <div className="auth-shell">
      <div className="auth-card">
        <div>
          <p className="eyebrow">Restoran Paneli</p>
          <h1>QR menünü canlı olarak yönet</h1>
          <p className="muted">Kategori, ürün, fiyat ve çeviri yönetimini tek panelden dakikalar içinde yönetin.</p>
        </div>
        <form className="auth-form" onSubmit={(event) => void handleSubmit(event)}>
          <label>E-posta<input value={email} onChange={(event) => setEmail(event.target.value)} type="email" /></label>
          <label>Şifre<input value={password} onChange={(event) => setPassword(event.target.value)} type="password" /></label>
          {error ? <p className="form-error">{error}</p> : null}
          <button type="submit" className="primary-button" disabled={loading}>{loading ? "Giriş yapılıyor..." : "Giriş Yap"}</button>
        </form>
      </div>
    </div>
  );
}

function AdminLayout() {
  const { session, logout } = useAuth();
  const roles = session?.user.roles ?? [];
  const menuItems = [
    { to: "/admin", label: "Genel Bakış", roles: ["RESTAURANT_OWNER", "BRANCH_MANAGER", "MENU_EDITOR"] },
    { to: "/admin/categories", label: "Kategoriler", roles: ["RESTAURANT_OWNER", "BRANCH_MANAGER", "MENU_EDITOR"] },
    { to: "/admin/items", label: "Ürünler", roles: ["RESTAURANT_OWNER", "BRANCH_MANAGER", "MENU_EDITOR"] },
    { to: "/admin/languages", label: "Diller", roles: ["RESTAURANT_OWNER", "BRANCH_MANAGER"] },
    { to: "/admin/qr", label: "QR Yönetimi", roles: ["RESTAURANT_OWNER", "BRANCH_MANAGER"] },
    { to: "/admin/theme", label: "Tema Ayarları", roles: ["RESTAURANT_OWNER", "BRANCH_MANAGER"] },
    { to: "/admin/restaurant", label: "Restoran Ayarları", roles: ["RESTAURANT_OWNER", "BRANCH_MANAGER"] }
  ].filter((item) => item.roles.some((role) => roles.includes(role)));

  return (
    <div className="admin-shell">
      <aside className="admin-sidebar">
        <div>
          <p className="eyebrow">Multi Language QR Menu</p>
          <h1>{session?.user.restaurantName}</h1>
          <p className="muted">Menü operasyonunu, QR çıktılarını ve tema görünümünü tek merkezden yönet.</p>
        </div>
        <nav className="admin-nav">
          {menuItems.map((item) => <NavLink key={item.to} to={item.to} end={item.to === "/admin"} className={({ isActive }) => isActive ? "nav-link active" : "nav-link"}>{item.label}</NavLink>)}
        </nav>
        <button type="button" className="secondary-button" onClick={() => void logout()}>Çıkış Yap</button>
      </aside>
      <main className="admin-main">
        <header className="admin-header">
          <div>
            <p className="eyebrow">Aktif kullanıcı</p>
            <h2>{session?.user.fullName}</h2>
          </div>
          <div className="badge-row">{roles.map((role) => <span key={role} className="role-badge">{role}</span>)}</div>
        </header>
        <Outlet />
      </main>
    </div>
  );
}

function DashboardPage() {
  const { session } = useAuth();
  const [health, setHealth] = useState<HealthPayload | null>(null);
  const [categories, setCategories] = useState<Category[]>([]);
  const [items, setItems] = useState<MenuItem[]>([]);
  const [error, setError] = useState("");

  useEffect(() => {
    request<HealthPayload>("/health", { method: "GET" }).then(setHealth).catch((exception) => setError(exception instanceof Error ? exception.message : "API durumu alınamadı."));
  }, []);

  useEffect(() => {
    if (!session) return;
    request<Category[]>("/api/menu-categories", { method: "GET" }, session.accessToken).then(setCategories).catch(() => undefined);
    request<MenuItem[]>("/api/menu-items", { method: "GET" }, session.accessToken).then(setItems).catch(() => undefined);
  }, [session]);

  return (
    <section className="content-grid">
      <article className="panel-card hero-card">
        <p className="eyebrow">Sprint 2-3 durumu</p>
        <h3>{session?.user.restaurantName}</h3>
        <p className="muted">Kategori, ürün, dil ve yayın akışları tek ürün deneyiminde birleşti.</p>
      </article>
      <article className="panel-card"><p className="eyebrow">API Durumu</p><h3>{health?.status ?? "Kontrol ediliyor"}</h3><p className="muted">{health?.serverTimeUtc ?? (error || "Backend bağlantısı test ediliyor.")}</p></article>
      <article className="panel-card"><p className="eyebrow">Kategori Sayısı</p><h3>{categories.length}</h3><p className="muted">Aktif ve pasif tüm menü kategorileri burada sayılır.</p></article>
      <article className="panel-card"><p className="eyebrow">Ürün Sayısı</p><h3>{items.length}</h3><p className="muted">Kategori filtreli ürün yönetimi ürün ekranından yapılabilir.</p></article>
    </section>
  );
}

function RestaurantSettingsPage() {
  const { session, refreshProfile } = useAuth();
  const [form, setForm] = useState<RestaurantSettings | null>(null);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");

  useEffect(() => {
    if (!session) return;
    request<RestaurantSettings>("/api/restaurants/current", { method: "GET" }, session.accessToken).then(setForm).catch((exception) => setError(exception instanceof Error ? exception.message : "Restoran verisi alınamadı."));
  }, [session]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!session || !form) return;
    setSaving(true);
    setMessage("");
    setError("");
    try {
      await request<RestaurantSettings>("/api/restaurants/current", { method: "PUT", body: JSON.stringify(form) }, session.accessToken);
      setMessage("Restoran ayarları kaydedildi.");
      await refreshProfile();
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Kaydetme başarısız oldu.");
    } finally {
      setSaving(false);
    }
  }

  if (!form) return <section className="panel-card">Restoran ayarları yükleniyor...</section>;

  return (
    <section className="panel-card">
      <div className="section-heading"><div><p className="eyebrow">Restoran Profili</p><h3>İşletme bilgilerini güncelle</h3></div><span className={form.isActive ? "status-pill success" : "status-pill muted"}>{form.isActive ? "Aktif" : "Pasif"}</span></div>
      <form className="settings-form" onSubmit={(event) => void handleSubmit(event)}>
        <label>Restoran adı<input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} /></label>
        <label>Telefon<input value={form.phone} onChange={(event) => setForm({ ...form, phone: event.target.value })} /></label>
        <label>WhatsApp<input value={form.whatsappPhone} onChange={(event) => setForm({ ...form, whatsappPhone: event.target.value })} /></label>
        <label>E-posta<input value={form.email} onChange={(event) => setForm({ ...form, email: event.target.value })} /></label>
        <label>Adres<textarea value={form.address} onChange={(event) => setForm({ ...form, address: event.target.value })} /></label>
        <label>Logo URL<input value={form.logoUrl} onChange={(event) => setForm({ ...form, logoUrl: event.target.value })} /></label>
        <label>Varsayılan dil<input value={form.defaultLanguage} onChange={(event) => setForm({ ...form, defaultLanguage: event.target.value.toLowerCase() })} /></label>
        <label>Para birimi<input value={form.currency} onChange={(event) => setForm({ ...form, currency: event.target.value.toUpperCase() })} /></label>
        {message ? <p className="form-success">{message}</p> : null}
        {error ? <p className="form-error">{error}</p> : null}
        <button className="primary-button" type="submit" disabled={saving}>{saving ? "Kaydediliyor..." : "Kaydet"}</button>
      </form>
    </section>
  );
}

function CategoryManagementPage() {
  const { session } = useAuth();
  const [categories, setCategories] = useState<Category[]>([]);
  const [form, setForm] = useState<CategoryForm>(createEmptyCategoryForm());
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");

  async function loadCategories() {
    if (!session) return;
    setLoading(true);
    try {
      const data = await request<Category[]>("/api/menu-categories", { method: "GET" }, session.accessToken);
      setCategories(data);
      if (form.id) {
        const match = data.find((category) => category.id === form.id);
        if (match) setForm(mapCategoryToForm(match));
      }
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Kategori listesi alınamadı.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { void loadCategories(); }, [session]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!session) return;
    setSaving(true);
    setMessage("");
    setError("");
    try {
      const path = form.id ? `/api/menu-categories/${form.id}` : "/api/menu-categories";
      const method = form.id ? "PUT" : "POST";
      await request<Category>(path, { method, body: JSON.stringify(form) }, session.accessToken);
      setMessage(form.id ? "Kategori güncellendi." : "Kategori eklendi.");
      setForm(createEmptyCategoryForm());
      await loadCategories();
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Kategori kaydedilemedi.");
    } finally {
      setSaving(false);
    }
  }

  async function removeCategory(categoryId: string) {
    if (!session || !window.confirm("Bu kategoriyi silmek istediğine emin misin?")) return;
    try {
      await request(`/api/menu-categories/${categoryId}`, { method: "DELETE" }, session.accessToken);
      if (form.id === categoryId) setForm(createEmptyCategoryForm());
      await loadCategories();
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Kategori silinemedi.");
    }
  }

  async function reorderCategory(index: number, direction: -1 | 1) {
    if (!session) return;
    const nextIndex = index + direction;
    if (nextIndex < 0 || nextIndex >= categories.length) return;
    const ordered = [...categories];
    const [moved] = ordered.splice(index, 1);
    ordered.splice(nextIndex, 0, moved);
    setCategories(ordered);
    try {
      await request("/api/menu-categories/reorder", { method: "PATCH", body: JSON.stringify({ orderedCategoryIds: ordered.map((category) => category.id) }) }, session.accessToken);
      await loadCategories();
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Kategori sıralaması güncellenemedi.");
    }
  }

  return (
    <section className="management-shell">
      <div className="management-list panel-card">
        <div className="section-heading"><div><p className="eyebrow">Sprint 2</p><h3>Kategori Yönetimi</h3></div><button type="button" className="secondary-button" onClick={() => setForm(createEmptyCategoryForm())}>Yeni Kategori</button></div>
        {loading ? <p className="muted">Kategoriler yükleniyor...</p> : null}
        {categories.map((category, index) => <article key={category.id} className={form.id === category.id ? "list-card active" : "list-card"}><div><h4>{category.name}</h4><p className="muted">{category.description || "Açıklama yok"}</p><div className="inline-badges"><span className="role-badge">Sıra #{category.sortOrder}</span><span className={category.isActive ? "status-pill success" : "status-pill muted"}>{category.isActive ? "Aktif" : "Pasif"}</span><span className="role-badge">{category.itemCount} ürün</span></div></div><div className="row-actions"><button type="button" className="tiny-button" onClick={() => void reorderCategory(index, -1)}>Yukarı</button><button type="button" className="tiny-button" onClick={() => void reorderCategory(index, 1)}>Aşağı</button><button type="button" className="tiny-button" onClick={() => setForm(mapCategoryToForm(category))}>Düzenle</button><button type="button" className="tiny-button danger" onClick={() => void removeCategory(category.id)}>Sil</button></div></article>)}
      </div>
      <div className="panel-card">
        <div className="section-heading"><div><p className="eyebrow">Kategori Formu</p><h3>{form.id ? "Kategoriyi düzenle" : "Yeni kategori oluştur"}</h3></div><span className="role-badge">TR / EN / DE / RU</span></div>
        <form className="settings-form" onSubmit={(event) => void handleSubmit(event)}>
          <label>Kategori adı<input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} /></label>
          <label>Slug<input value={form.slug} onChange={(event) => setForm({ ...form, slug: event.target.value })} /></label>
          <label>Açıklama<textarea value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} /></label>
          <label>Görsel URL<input value={form.imageUrl} onChange={(event) => setForm({ ...form, imageUrl: event.target.value })} /></label>
          <label className="checkbox-row"><input type="checkbox" checked={form.isActive} onChange={(event) => setForm({ ...form, isActive: event.target.checked })} />Kategori aktif</label>
          <div className="translation-grid">{form.translations.map((translation, index) => <div key={translation.languageCode} className="translation-card"><p className="eyebrow">{translation.languageCode.toUpperCase()}</p><label>Ad<input value={translation.name} onChange={(event) => { const next = [...form.translations]; next[index] = { ...translation, name: event.target.value }; setForm({ ...form, translations: next }); }} /></label><label>Açıklama<textarea value={translation.description} onChange={(event) => { const next = [...form.translations]; next[index] = { ...translation, description: event.target.value }; setForm({ ...form, translations: next }); }} /></label></div>)}</div>
          {message ? <p className="form-success">{message}</p> : null}
          {error ? <p className="form-error">{error}</p> : null}
          <button className="primary-button" type="submit" disabled={saving}>{saving ? "Kaydediliyor..." : form.id ? "Kategoriyi Güncelle" : "Kategori Ekle"}</button>
        </form>
      </div>
    </section>
  );
}

function MenuItemManagementPage() {
  const { session } = useAuth();
  const [categories, setCategories] = useState<Category[]>([]);
  const [items, setItems] = useState<MenuItem[]>([]);
  const [selectedCategoryId, setSelectedCategoryId] = useState("");
  const [form, setForm] = useState<MenuItemForm>(createEmptyItemForm());
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");

  async function loadDependencies() {
    if (!session) return;
    setLoading(true);
    try {
      const [categoryData, itemData] = await Promise.all([
        request<Category[]>("/api/menu-categories", { method: "GET" }, session.accessToken),
        request<MenuItem[]>(selectedCategoryId ? `/api/menu-items?categoryId=${selectedCategoryId}` : "/api/menu-items", { method: "GET" }, session.accessToken)
      ]);
      setCategories(categoryData);
      setItems(itemData);
      if (!form.categoryId && categoryData[0]) setForm((current) => createEmptyItemForm(current.categoryId || categoryData[0].id));
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Ürün verileri alınamadı.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { void loadDependencies(); }, [session, selectedCategoryId]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!session) return;
    setSaving(true);
    setMessage("");
    setError("");

    const payload = {
      ...form,
      price: Number(form.price || 0),
      discountedPrice: form.discountedPrice ? Number(form.discountedPrice) : null,
      preparationTimeMinutes: form.preparationTimeMinutes ? Number(form.preparationTimeMinutes) : null,
      calories: form.calories ? Number(form.calories) : null,
      spiceLevel: Number(form.spiceLevel || 0)
    };

    try {
      const path = form.id ? `/api/menu-items/${form.id}` : "/api/menu-items";
      const method = form.id ? "PUT" : "POST";
      await request<MenuItem>(path, { method, body: JSON.stringify(payload) }, session.accessToken);
      setMessage(form.id ? "Ürün güncellendi." : "Ürün eklendi.");
      setForm(createEmptyItemForm(selectedCategoryId || categories[0]?.id || ""));
      await loadDependencies();
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Ürün kaydedilemedi.");
    } finally {
      setSaving(false);
    }
  }

  async function removeItem(itemId: string) {
    if (!session || !window.confirm("Bu ürünü silmek istediğine emin misin?")) return;
    try {
      await request(`/api/menu-items/${itemId}`, { method: "DELETE" }, session.accessToken);
      if (form.id === itemId) setForm(createEmptyItemForm(selectedCategoryId || categories[0]?.id || ""));
      await loadDependencies();
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Ürün silinemedi.");
    }
  }

  async function reorderItem(item: MenuItem, direction: -1 | 1) {
    if (!session) return;
    const categoryItems = items.filter((entry) => entry.categoryId === item.categoryId);
    const index = categoryItems.findIndex((entry) => entry.id === item.id);
    const nextIndex = index + direction;
    if (index < 0 || nextIndex < 0 || nextIndex >= categoryItems.length) return;
    const ordered = [...categoryItems];
    const [moved] = ordered.splice(index, 1);
    ordered.splice(nextIndex, 0, moved);
    try {
      await request("/api/menu-items/reorder", { method: "PATCH", body: JSON.stringify({ categoryId: item.categoryId, orderedItemIds: ordered.map((entry) => entry.id) }) }, session.accessToken);
      await loadDependencies();
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Ürün sıralaması güncellenemedi.");
    }
  }

  return (
    <section className="management-shell">
      <div className="management-list panel-card">
        <div className="section-heading"><div><p className="eyebrow">Sprint 3</p><h3>Ürün Yönetimi</h3></div><button type="button" className="secondary-button" onClick={() => setForm(createEmptyItemForm(selectedCategoryId || categories[0]?.id || ""))}>Yeni Ürün</button></div>
        <label>Kategori filtresi<select value={selectedCategoryId} onChange={(event) => setSelectedCategoryId(event.target.value)}><option value="">Tüm kategoriler</option>{categories.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}</select></label>
        {loading ? <p className="muted">Ürünler yükleniyor...</p> : null}
        {items.map((item) => <article key={item.id} className={form.id === item.id ? "list-card active" : "list-card"}><div><h4>{item.name}</h4><p className="muted">{item.categoryName} · {item.price} {item.currency}</p><div className="inline-badges"><span className={item.isAvailable ? "status-pill success" : "status-pill muted"}>{item.isAvailable ? "Stokta" : "Tükendi"}</span><span className={item.isActive ? "status-pill success" : "status-pill muted"}>{item.isActive ? "Aktif" : "Pasif"}</span>{item.isFeatured ? <span className="role-badge">Öne Çıkan</span> : null}</div></div><div className="row-actions"><button type="button" className="tiny-button" onClick={() => void reorderItem(item, -1)}>Yukarı</button><button type="button" className="tiny-button" onClick={() => void reorderItem(item, 1)}>Aşağı</button><button type="button" className="tiny-button" onClick={() => setForm(mapItemToForm(item))}>Düzenle</button><button type="button" className="tiny-button danger" onClick={() => void removeItem(item.id)}>Sil</button></div></article>)}
      </div>
      <div className="panel-card">
        <div className="section-heading"><div><p className="eyebrow">Ürün Formu</p><h3>{form.id ? "Ürünü düzenle" : "Yeni ürün oluştur"}</h3></div><span className="role-badge">Fiyat · Etiket · Çeviri</span></div>
        <form className="settings-form" onSubmit={(event) => void handleSubmit(event)}>
          <div className="two-column-grid"><label>Kategori<select value={form.categoryId} onChange={(event) => setForm({ ...form, categoryId: event.target.value })}>{categories.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}</select></label><label>Ürün adı<input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} /></label></div>
          <div className="two-column-grid"><label>Slug<input value={form.slug} onChange={(event) => setForm({ ...form, slug: event.target.value })} /></label><label>Görsel URL<input value={form.imageUrl} onChange={(event) => setForm({ ...form, imageUrl: event.target.value })} /></label></div>
          <label>Açıklama<textarea value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} /></label>
          <div className="two-column-grid"><label>Fiyat<input value={form.price} onChange={(event) => setForm({ ...form, price: event.target.value })} /></label><label>İndirimli fiyat<input value={form.discountedPrice} onChange={(event) => setForm({ ...form, discountedPrice: event.target.value })} /></label></div>
          <div className="two-column-grid"><label>Para birimi<input value={form.currency} onChange={(event) => setForm({ ...form, currency: event.target.value.toUpperCase() })} /></label><label>Baharat seviyesi (0-5)<input value={form.spiceLevel} onChange={(event) => setForm({ ...form, spiceLevel: event.target.value })} /></label></div>
          <div className="two-column-grid"><label>Hazırlık süresi<input value={form.preparationTimeMinutes} onChange={(event) => setForm({ ...form, preparationTimeMinutes: event.target.value })} /></label><label>Kalori<input value={form.calories} onChange={(event) => setForm({ ...form, calories: event.target.value })} /></label></div>
          <div className="checkbox-grid"><label className="checkbox-row"><input type="checkbox" checked={form.isVegetarian} onChange={(event) => setForm({ ...form, isVegetarian: event.target.checked })} />Vejetaryen</label><label className="checkbox-row"><input type="checkbox" checked={form.isVegan} onChange={(event) => setForm({ ...form, isVegan: event.target.checked })} />Vegan</label><label className="checkbox-row"><input type="checkbox" checked={form.isGlutenFree} onChange={(event) => setForm({ ...form, isGlutenFree: event.target.checked })} />Glutensiz</label><label className="checkbox-row"><input type="checkbox" checked={form.isFeatured} onChange={(event) => setForm({ ...form, isFeatured: event.target.checked })} />Öne çıkan</label><label className="checkbox-row"><input type="checkbox" checked={form.isAvailable} onChange={(event) => setForm({ ...form, isAvailable: event.target.checked })} />Stokta</label><label className="checkbox-row"><input type="checkbox" checked={form.isActive} onChange={(event) => setForm({ ...form, isActive: event.target.checked })} />Yayında</label></div>
          <div className="translation-grid">{form.translations.map((translation, index) => <div key={translation.languageCode} className="translation-card"><p className="eyebrow">{translation.languageCode.toUpperCase()}</p><label>Ad<input value={translation.name} onChange={(event) => { const next = [...form.translations]; next[index] = { ...translation, name: event.target.value }; setForm({ ...form, translations: next }); }} /></label><label>Açıklama<textarea value={translation.description} onChange={(event) => { const next = [...form.translations]; next[index] = { ...translation, description: event.target.value }; setForm({ ...form, translations: next }); }} /></label></div>)}</div>
          {message ? <p className="form-success">{message}</p> : null}
          {error ? <p className="form-error">{error}</p> : null}
          <button className="primary-button" type="submit" disabled={saving}>{saving ? "Kaydediliyor..." : form.id ? "Ürünü Güncelle" : "Ürün Ekle"}</button>
        </form>
      </div>
    </section>
  );
}

function LanguageSettingsPage() {
  const { session, refreshProfile } = useAuth();
  const [languages, setLanguages] = useState<LanguageOption[]>([]);
  const [enabledCodes, setEnabledCodes] = useState<string[]>([]);
  const [defaultLanguage, setDefaultLanguage] = useState("tr");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");

  useEffect(() => {
    if (!session) return;
    setLoading(true);
    Promise.all([
      request<LanguageOption[]>("/api/languages", { method: "GET" }, session.accessToken),
      request<LanguageOption[]>("/api/restaurants/languages", { method: "GET" }, session.accessToken)
    ]).then(([available, configured]) => {
      const configuredCodes = configured.filter((language) => language.isEnabled).map((language) => language.code);
      setLanguages(available.map((language) => configured.find((entry) => entry.code === language.code) ?? language));
      setEnabledCodes(configuredCodes);
      setDefaultLanguage(configured.find((language) => language.isDefault)?.code ?? configuredCodes[0] ?? "tr");
    }).catch((exception) => {
      setError(exception instanceof Error ? exception.message : "Dil ayarları alınamadı.");
    }).finally(() => setLoading(false));
  }, [session]);

  function toggleLanguage(code: string) {
    setEnabledCodes((current) => {
      const exists = current.includes(code);
      const next = exists ? current.filter((entry) => entry !== code) : [...current, code];
      if (!next.includes(defaultLanguage) && next[0]) {
        setDefaultLanguage(next[0]);
      }
      return next;
    });
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!session) return;
    setSaving(true);
    setMessage("");
    setError("");
    try {
      const payload = await request<LanguageOption[]>("/api/restaurants/languages", {
        method: "PUT",
        body: JSON.stringify({ enabledLanguageCodes: enabledCodes, defaultLanguage })
      }, session.accessToken);
      setEnabledCodes(payload.filter((language) => language.isEnabled).map((language) => language.code));
      setDefaultLanguage(payload.find((language) => language.isDefault)?.code ?? defaultLanguage);
      setMessage("Restoran dil ayarları güncellendi.");
      await refreshProfile();
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Dil ayarları kaydedilemedi.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="content-grid">
      <article className="panel-card">
        <div className="section-heading">
          <div>
            <p className="eyebrow">Sprint 5</p>
            <h3>Public Dil Ayarları</h3>
          </div>
          <span className="role-badge">TR / EN / DE / RU</span>
        </div>
        <p className="muted">Public menüde görünecek dilleri ve varsayılan dili buradan belirleyebilirsin.</p>
        <form className="settings-form" onSubmit={(event) => void handleSubmit(event)}>
          {loading ? <p className="muted">Dil ayarları yükleniyor...</p> : null}
          <div className="language-stack">
            {languages.map((language) => {
              const checked = enabledCodes.includes(language.code);
              return (
                <label key={language.code} className="language-row">
                  <div>
                    <strong>{language.name}</strong>
                    <p className="muted">{language.code.toUpperCase()}</p>
                  </div>
                  <div className="row-actions">
                    <label className="checkbox-row"><input type="checkbox" checked={checked} onChange={() => toggleLanguage(language.code)} />Aktif</label>
                    <label className="checkbox-row"><input type="radio" name="defaultLanguage" checked={defaultLanguage === language.code} disabled={!checked} onChange={() => setDefaultLanguage(language.code)} />Varsayılan</label>
                  </div>
                </label>
              );
            })}
          </div>
          {enabledCodes.length === 0 ? <p className="form-error">En az bir dil aktif kalmalıdır.</p> : null}
          {message ? <p className="form-success">{message}</p> : null}
          {error ? <p className="form-error">{error}</p> : null}
          <button className="primary-button" type="submit" disabled={saving || enabledCodes.length === 0}>{saving ? "Kaydediliyor..." : "Dil Ayarlarını Kaydet"}</button>
        </form>
      </article>
      <article className="panel-card">
        <p className="eyebrow">Çeviri Kalitesi</p>
        <h3>Admin Panel Rehberi</h3>
        <p className="muted">Kategori ve ürün formlarındaki çeviri alanları zaten aktif. Public menüde eksik çeviri varsa sistem otomatik olarak restoranın varsayılan diline geri düşer.</p>
        <div className="inline-badges">
          <span className="role-badge">Fallback destekli</span>
          <span className="role-badge">Turist dostu</span>
          <span className="role-badge">SaaS hazır</span>
        </div>
      </article>
    </section>
  );
}

function QrManagementPage() {
  const { session } = useAuth();
  const [tables, setTables] = useState<RestaurantTable[]>([]);
  const [generalQr, setGeneralQr] = useState<QrCodeCard | null>(null);
  const [printableCards, setPrintableCards] = useState<QrCodeCard[]>([]);
  const [form, setForm] = useState(createEmptyTableForm());
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");

  async function loadQrData() {
    if (!session) return;
    setLoading(true);
    try {
      const [tableData, generalData, printableData] = await Promise.all([
        request<RestaurantTable[]>("/api/tables", { method: "GET" }, session.accessToken),
        request<QrCodeCard>("/api/qr/general", { method: "GET" }, session.accessToken),
        request<QrCodeCard[]>("/api/qr/printable", { method: "GET" }, session.accessToken)
      ]);
      setTables(tableData);
      setGeneralQr(generalData);
      setPrintableCards(printableData);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "QR verileri alınamadı.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { void loadQrData(); }, [session]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!session) return;
    setSaving(true);
    setMessage("");
    setError("");
    try {
      const payload = { tableNumber: Number(form.tableNumber), tableName: form.tableName, isActive: form.isActive };
      if (form.id) {
        await request<RestaurantTable>("/api/tables/" + form.id, { method: "PUT", body: JSON.stringify(payload) }, session.accessToken);
        setMessage("Masa güncellendi.");
      } else {
        await request<RestaurantTable>("/api/tables", { method: "POST", body: JSON.stringify(payload) }, session.accessToken);
        setMessage("Masa eklendi.");
      }
      setForm(createEmptyTableForm());
      await loadQrData();
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Masa kaydedilemedi.");
    } finally {
      setSaving(false);
    }
  }

  async function deleteTable(tableId: string) {
    if (!session || !window.confirm("Bu masayı silmek istediğine emin misin?")) return;
    try {
      await request("/api/tables/" + tableId, { method: "DELETE" }, session.accessToken);
      if (form.id === tableId) setForm(createEmptyTableForm());
      await loadQrData();
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Masa silinemedi.");
    }
  }

  async function regenerateTableQr(tableId: string) {
    if (!session) return;
    try {
      await request<RestaurantTable>("/api/tables/" + tableId + "/qr/regenerate", { method: "POST" }, session.accessToken);
      setMessage("Masa QR kodu yenilendi.");
      await loadQrData();
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Masa QR kodu yenilenemedi.");
    }
  }

  async function regenerateGeneralQr() {
    if (!session) return;
    try {
      await request<QrCodeCard>("/api/qr/general/regenerate", { method: "POST" }, session.accessToken);
      setMessage("Genel QR kodu yenilendi.");
      await loadQrData();
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Genel QR kodu yenilenemedi.");
    }
  }

  function printCards() {
    const popup = window.open("", "_blank", "width=1280,height=900");
    if (!popup) return;
    popup.document.open();
    popup.document.write(buildPrintableHtml(printableCards, session?.user.restaurantName ?? "QR Menu"));
    popup.document.close();
    popup.focus();
    popup.print();
  }

  return (
    <section className="content-grid">
      <article className="panel-card">
        <div className="section-heading">
          <div>
            <p className="eyebrow">Sprint 6</p>
            <h3>QR Yönetimi</h3>
          </div>
          <button type="button" className="secondary-button" onClick={() => setForm(createEmptyTableForm())}>Yeni Masa</button>
        </div>
        <p className="muted">Genel menü QR kodunu, masa bazlı QR çıktılarını ve yazdırılabilir kartları buradan yönetebilirsin.</p>
        {generalQr ? (
          <div className="qr-hero-card">
            <img src={generalQr.qrImageUrl} alt={generalQr.label} />
            <div>
              <strong>{generalQr.label}</strong>
              <p className="muted">{generalQr.targetUrl}</p>
              <div className="row-actions">
                <a className="tiny-button" href={generalQr.qrImageUrl} target="_blank" rel="noreferrer">PNG Aç</a>
                <button type="button" className="tiny-button" onClick={() => void regenerateGeneralQr()}>QR Yenile</button>
                <button type="button" className="tiny-button" onClick={printCards}>PDF / Yazdır</button>
              </div>
            </div>
          </div>
        ) : null}
        {loading ? <p className="muted">QR kayıtları yükleniyor...</p> : null}
        <div className="qr-table-list">
          {tables.map((table) => (
            <article key={table.id} className={form.id === table.id ? "list-card active" : "list-card"}>
              <div>
                <h4>{"Masa " + table.tableNumber + (table.tableName ? " · " + table.tableName : "")}</h4>
                <p className="muted">{table.targetUrl}</p>
                <div className="inline-badges">
                  <span className={table.isActive ? "status-pill success" : "status-pill muted"}>{table.isActive ? "Aktif" : "Pasif"}</span>
                </div>
              </div>
              <div className="row-actions">
                <a className="tiny-button" href={table.qrImageUrl} target="_blank" rel="noreferrer">PNG</a>
                <button type="button" className="tiny-button" onClick={() => void regenerateTableQr(table.id)}>QR Yenile</button>
                <button type="button" className="tiny-button" onClick={() => setForm({ id: table.id, tableNumber: String(table.tableNumber), tableName: table.tableName, isActive: table.isActive })}>Düzenle</button>
                <button type="button" className="tiny-button danger" onClick={() => void deleteTable(table.id)}>Sil</button>
              </div>
            </article>
          ))}
        </div>
      </article>

      <article className="panel-card">
        <div className="section-heading">
          <div>
            <p className="eyebrow">Masa Formu</p>
            <h3>{form.id ? "Masayı düzenle" : "Yeni masa oluştur"}</h3>
          </div>
          <span className="role-badge">Masa bazlı QR</span>
        </div>
        <form className="settings-form" onSubmit={(event) => void handleSubmit(event)}>
          <div className="two-column-grid">
            <label>Masa numarası<input value={form.tableNumber} onChange={(event) => setForm({ ...form, tableNumber: event.target.value })} /></label>
            <label>Masa etiketi<input value={form.tableName} onChange={(event) => setForm({ ...form, tableName: event.target.value })} placeholder="Örn. Teras / VIP / Havuz" /></label>
          </div>
          <label className="checkbox-row"><input type="checkbox" checked={form.isActive} onChange={(event) => setForm({ ...form, isActive: event.target.checked })} />Masa aktif</label>
          {message ? <p className="form-success">{message}</p> : null}
          {error ? <p className="form-error">{error}</p> : null}
          <button className="primary-button" type="submit" disabled={saving}>{saving ? "Kaydediliyor..." : form.id ? "Masayı Güncelle" : "Masa Ekle"}</button>
        </form>
      </article>
    </section>
  );
}

function ThemeSettingsPage() {
  const { session } = useAuth();
  const [theme, setTheme] = useState<ThemeSettings>(createDefaultTheme());
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");

  useEffect(() => {
    if (!session) return;
    setLoading(true);
    request<ThemeSettings>("/api/theme", { method: "GET" }, session.accessToken)
      .then((data) => setTheme({ ...createDefaultTheme(), ...data }))
      .catch((exception) => setError(exception instanceof Error ? exception.message : "Tema ayarları alınamadı."))
      .finally(() => setLoading(false));
  }, [session]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!session) return;
    setSaving(true);
    setMessage("");
    setError("");
    try {
      const payload = await request<ThemeSettings>("/api/theme", { method: "PUT", body: JSON.stringify(theme) }, session.accessToken);
      setTheme(payload);
      setMessage("Tema ayarları kaydedildi.");
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Tema ayarları kaydedilemedi.");
    } finally {
      setSaving(false);
    }
  }

  const previewStyle: CSSProperties = { ["--theme-primary" as string]: theme.primaryColor, ["--theme-secondary" as string]: theme.secondaryColor, ["--theme-font" as string]: theme.fontFamily } as CSSProperties;

  return (
    <section className="content-grid">
      <article className="panel-card">
        <div className="section-heading">
          <div>
            <p className="eyebrow">Sprint 7</p>
            <h3>Tema ve Marka Ayarları</h3>
          </div>
          <span className="role-badge">Canlı önizleme</span>
        </div>
        <form className="settings-form" onSubmit={(event) => void handleSubmit(event)}>
          {loading ? <p className="muted">Tema ayarları yükleniyor...</p> : null}
          <div className="two-column-grid">
            <label>Logo URL<input value={theme.logoUrl} onChange={(event) => setTheme({ ...theme, logoUrl: event.target.value })} /></label>
            <label>Kapak görseli URL<input value={theme.coverImageUrl} onChange={(event) => setTheme({ ...theme, coverImageUrl: event.target.value })} /></label>
          </div>
          <div className="two-column-grid">
            <label>Ana renk<input type="color" value={theme.primaryColor} onChange={(event) => setTheme({ ...theme, primaryColor: event.target.value })} /></label>
            <label>İkincil renk<input type="color" value={theme.secondaryColor} onChange={(event) => setTheme({ ...theme, secondaryColor: event.target.value })} /></label>
          </div>
          <div className="two-column-grid">
            <label>Font<select value={theme.fontFamily} onChange={(event) => setTheme({ ...theme, fontFamily: event.target.value })}><option value="Manrope">Manrope</option><option value="Space Grotesk">Space Grotesk</option><option value="Georgia">Georgia</option></select></label>
            <label>Menü düzeni<select value={theme.menuLayout} onChange={(event) => setTheme({ ...theme, menuLayout: event.target.value as "cards" | "list" })}><option value="cards">Kart görünümü</option><option value="list">Liste görünümü</option></select></label>
          </div>
          <div className="checkbox-grid">
            <label className="checkbox-row"><input type="checkbox" checked={theme.showWhatsappButton} onChange={(event) => setTheme({ ...theme, showWhatsappButton: event.target.checked })} />WhatsApp butonu</label>
            <label className="checkbox-row"><input type="checkbox" checked={theme.showGoogleReviewButton} onChange={(event) => setTheme({ ...theme, showGoogleReviewButton: event.target.checked })} />Google yorum butonu</label>
          </div>
          <label>Google yorum linki<input value={theme.googleReviewUrl} onChange={(event) => setTheme({ ...theme, googleReviewUrl: event.target.value })} /></label>
          {message ? <p className="form-success">{message}</p> : null}
          {error ? <p className="form-error">{error}</p> : null}
          <button className="primary-button" type="submit" disabled={saving}>{saving ? "Kaydediliyor..." : "Temayı Kaydet"}</button>
        </form>
      </article>

      <article className="panel-card">
        <div className="section-heading"><div><p className="eyebrow">Public Önizleme</p><h3>Satılabilir görünüm kontrolü</h3></div></div>
        <div className={"theme-preview-card " + (theme.menuLayout === "list" ? "is-list" : "")} style={previewStyle}>
          {theme.coverImageUrl ? <div className="theme-preview-cover" style={{ backgroundImage: "url(" + theme.coverImageUrl + ")" }} /> : null}
          <div className="theme-preview-header">
            {theme.logoUrl ? <img src={theme.logoUrl} alt="Logo" className="theme-preview-logo" /> : null}
            <div>
              <p className="eyebrow">Marka Deneyimi</p>
              <h3>Demo QR Bistro</h3>
              <p className="muted">Renk, font, CTA ve layout ayarları menü algısını doğrudan etkiler.</p>
            </div>
          </div>
          <div className="inline-badges">
            <span className="role-badge">{theme.fontFamily}</span>
            <span className="role-badge">{theme.menuLayout === "cards" ? "Kart" : "Liste"}</span>
            {theme.showWhatsappButton ? <span className="role-badge">WhatsApp Açık</span> : null}
            {theme.showGoogleReviewButton ? <span className="role-badge">Yorum CTA</span> : null}
          </div>
        </div>
      </article>
    </section>
  );
}

function PublicLayout() {
  const { slug } = useParams();
  const location = useLocation();
  const [restaurant, setRestaurant] = useState<PublicRestaurant | null>(null);
  const [menu, setMenu] = useState<PublicMenuPayload | null>(null);
  const [theme, setTheme] = useState<ThemeSettings>(createDefaultTheme());
  const [language, setLanguage] = useState("");
  const [search, setSearch] = useState("");
  const deferredSearch = useDeferredValue(search);
  const [selectedCategoryId, setSelectedCategoryId] = useState("");
  const [selectedItem, setSelectedItem] = useState<PublicMenuItem | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!slug) return;
    request<PublicRestaurant>("/public/restaurants/" + slug, { method: "GET" })
      .then((data) => {
        setRestaurant(data);
        const saved = window.localStorage.getItem("mlqm-public-lang-" + slug) ?? "";
        const nextLanguage = data.activeLanguages.some((entry) => entry.code === saved) ? saved : data.selectedLanguage || data.defaultLanguage;
        setLanguage(nextLanguage);
      })
      .catch((exception) => setError(exception instanceof Error ? exception.message : "Menü yüklenemedi."));

    request<ThemeSettings>("/public/restaurants/" + slug + "/theme", { method: "GET" })
      .then((data) => setTheme({ ...createDefaultTheme(), ...data }))
      .catch(() => undefined);
  }, [slug]);

  useEffect(() => {
    if (!slug || !language) return;
    setLoading(true);
    request<PublicMenuPayload>("/public/restaurants/" + slug + "/menu?lang=" + language + "&search=" + encodeURIComponent(deferredSearch), { method: "GET" })
      .then((data) => {
        setMenu(data);
        setRestaurant(data.restaurant);
        window.localStorage.setItem("mlqm-public-lang-" + slug, data.languageCode);
      })
      .catch((exception) => setError(exception instanceof Error ? exception.message : "Menü yüklenemedi."))
      .finally(() => setLoading(false));
  }, [slug, language, deferredSearch]);

  useEffect(() => {
    const firstCategoryId = menu?.categories[0]?.id ?? "";
    if (!selectedCategoryId || !menu?.categories.some((category) => category.id === selectedCategoryId)) setSelectedCategoryId(firstCategoryId);
  }, [menu, selectedCategoryId]);

  const query = new URLSearchParams(location.search);
  const tableNumber = query.get("table");
  const tableLabel = query.get("label");
  const visibleCategories = menu?.categories.filter((category) => !selectedCategoryId || category.id === selectedCategoryId) ?? [];
  const whatsappLink = theme.showWhatsappButton && restaurant?.whatsappPhone ? "https://wa.me/" + normalizePhoneLink(restaurant.whatsappPhone) : "";
  const phoneLink = restaurant?.phone ? "tel:" + normalizePhoneLink(restaurant.phone) : "";
  const reviewLink = theme.showGoogleReviewButton && theme.googleReviewUrl ? theme.googleReviewUrl : "";
  const shellStyle: CSSProperties = { ["--theme-primary" as string]: theme.primaryColor, ["--theme-secondary" as string]: theme.secondaryColor, ["--theme-font" as string]: theme.fontFamily } as CSSProperties;

  return (
    <div className={"public-menu-shell " + (theme.menuLayout === "list" ? "layout-list" : "layout-cards")} style={shellStyle}>
      {theme.coverImageUrl ? <div className="public-cover-banner" style={{ backgroundImage: "linear-gradient(135deg, rgba(19,34,56,0.35), rgba(0,0,0,0.12)), url(" + theme.coverImageUrl + ")" }} /> : null}
      <section className="public-menu-page">
        <header className="public-topbar themed-surface">
          <div>
            <p className="eyebrow">QR Menü</p>
            <h1>{restaurant?.name ?? slug}</h1>
            <p className="muted">{restaurant?.address || "Mobil, hızlı ve şık menü deneyimi"}</p>
            {(tableNumber || tableLabel) ? <div className="inline-badges"><span className="role-badge">{"Masa " + (tableNumber ?? "-") + (tableLabel ? " · " + tableLabel : "")}</span></div> : null}
          </div>
          <div className="topbar-side">
            {(theme.logoUrl || restaurant?.logoUrl) ? <img className="brand-logo" src={theme.logoUrl || restaurant?.logoUrl} alt={restaurant?.name ?? "Logo"} /> : null}
            <div className="inline-badges">
              {restaurant?.activeLanguages.map((entry) => <button key={entry.code} type="button" className={language === entry.code ? "tiny-button active-chip" : "tiny-button"} onClick={() => setLanguage(entry.code)}>{entry.code.toUpperCase()}</button>)}
            </div>
          </div>
        </header>

        <section className="public-hero-card themed-hero">
          <div>
            <p className="eyebrow">Bugünün Menüsü</p>
            <h2>QR okut, ara, seç ve keşfet</h2>
            <p className="muted">Aktif kategori ve ürünler seçilen dilde anında görünür.</p>
          </div>
          <div className="cta-row">
            {whatsappLink ? <a className="primary-button" href={whatsappLink} target="_blank" rel="noreferrer">WhatsApp</a> : null}
            {phoneLink ? <a className="secondary-button" href={phoneLink}>Ara</a> : null}
            {reviewLink ? <a className="secondary-button" href={reviewLink} target="_blank" rel="noreferrer">Yorum Bırak</a> : null}
          </div>
        </section>

        <label>
          Ürün ara
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Örn. kahve, burger, tatlı" />
        </label>

        <div className="category-strip">
          {menu?.categories.map((category) => <button key={category.id} type="button" className={selectedCategoryId === category.id ? "category-chip active-chip" : "category-chip"} onClick={() => setSelectedCategoryId(category.id)}>{category.name}</button>)}
        </div>

        {loading ? <section className="public-menu-card"><p className="muted">Menü yükleniyor...</p></section> : null}
        {error ? <section className="public-menu-card"><p className="form-error">{error}</p></section> : null}

        {visibleCategories.map((category) => (
          <section key={category.id} className="public-section-card themed-surface">
            <div className="section-heading">
              <div>
                <p className="eyebrow">Kategori</p>
                <h3>{category.name}</h3>
              </div>
              <span className="role-badge">{category.items.length} ürün</span>
            </div>
            {category.description ? <p className="muted">{category.description}</p> : null}
            <div className={"public-item-grid " + (theme.menuLayout === "list" ? "list-layout" : "")}>
              {category.items.map((item) => (
                <button key={item.id} type="button" className="public-item-card themed-surface" onClick={() => setSelectedItem(item)}>
                  <div className="public-item-copy">
                    <div className="section-heading compact-row">
                      <strong>{item.name}</strong>
                      <span className={item.isAvailable ? "status-pill success" : "status-pill muted"}>{item.isAvailable ? "Serviste" : "Tükendi"}</span>
                    </div>
                    <p className="muted">{item.description}</p>
                    <div className="inline-badges">
                      {item.isFeatured ? <span className="role-badge">Öne Çıkan</span> : null}
                      {item.isVegetarian ? <span className="role-badge">Vejetaryen</span> : null}
                      {item.isVegan ? <span className="role-badge">Vegan</span> : null}
                      {item.isGlutenFree ? <span className="role-badge">Glutensiz</span> : null}
                    </div>
                  </div>
                  <div className="price-stack">
                    {item.discountedPrice != null ? <span className="price-old">{formatMoney(item.price, item.currency)}</span> : null}
                    <strong>{formatMoney(item.discountedPrice ?? item.price, item.currency)}</strong>
                  </div>
                </button>
              ))}
            </div>
          </section>
        ))}
      </section>

      {selectedItem ? (
        <div className="modal-backdrop" onClick={() => setSelectedItem(null)}>
          <div className="modal-card" onClick={(event) => event.stopPropagation()}>
            <div className="section-heading">
              <div>
                <p className="eyebrow">Ürün Detayı</p>
                <h3>{selectedItem.name}</h3>
              </div>
              <button type="button" className="tiny-button" onClick={() => setSelectedItem(null)}>Kapat</button>
            </div>
            <p className="muted">{selectedItem.description}</p>
            <div className="inline-badges">
              {selectedItem.preparationTimeMinutes ? <span className="role-badge">{selectedItem.preparationTimeMinutes} dk</span> : null}
              {selectedItem.calories ? <span className="role-badge">{selectedItem.calories} kcal</span> : null}
              <span className="role-badge">Baharat {selectedItem.spiceLevel}/5</span>
            </div>
            <div className="price-stack large-price">
              {selectedItem.discountedPrice != null ? <span className="price-old">{formatMoney(selectedItem.price, selectedItem.currency)}</span> : null}
              <strong>{formatMoney(selectedItem.discountedPrice ?? selectedItem.price, selectedItem.currency)}</strong>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}

export default function App() {
  return (
    <AuthProvider>
      <Routes>
        <Route path="/" element={<Navigate to="/admin" replace />} />
        <Route path="/login" element={<LoginPage />} />
        <Route element={<ProtectedRoute />}>
          <Route path="/admin" element={<AdminLayout />}>
            <Route index element={<DashboardPage />} />
            <Route path="categories" element={<CategoryManagementPage />} />
            <Route path="items" element={<MenuItemManagementPage />} />
            <Route path="languages" element={<LanguageSettingsPage />} />
            <Route path="restaurant" element={<RestaurantSettingsPage />} />
          </Route>
        </Route>
        <Route path="/menu/:slug" element={<PublicLayout />} />
      </Routes>
    </AuthProvider>
  );
}




