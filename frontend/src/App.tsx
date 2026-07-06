import {
  FormEvent,
  createContext,
  useContext,
  useDeferredValue,
  useEffect,
  useMemo,
  useState,
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
      setError(exception instanceof Error ? exception.message : "Giriþ baþarýsýz oldu.");
    }
  }

  return (
    <div className="auth-shell">
      <div className="auth-card">
        <div>
          <p className="eyebrow">Restoran Paneli</p>
          <h1>QR menünü canlý olarak yönet</h1>
          <p className="muted">Kategori, ürün, fiyat ve çeviri yönetimi artýk panelde yer alýyor.</p>
        </div>
        <form className="auth-form" onSubmit={(event) => void handleSubmit(event)}>
          <label>E-posta<input value={email} onChange={(event) => setEmail(event.target.value)} type="email" /></label>
          <label>Þifre<input value={password} onChange={(event) => setPassword(event.target.value)} type="password" /></label>
          {error ? <p className="form-error">{error}</p> : null}
          <button type="submit" className="primary-button" disabled={loading}>{loading ? "Giriþ yapýlýyor..." : "Giriþ Yap"}</button>
        </form>
      </div>
    </div>
  );
}

function AdminLayout() {
  const { session, logout } = useAuth();
  const roles = session?.user.roles ?? [];
  const menuItems = [
    { to: "/admin", label: "Genel Bakýþ", roles: ["RESTAURANT_OWNER", "BRANCH_MANAGER", "MENU_EDITOR"] },
    { to: "/admin/categories", label: "Kategoriler", roles: ["RESTAURANT_OWNER", "BRANCH_MANAGER", "MENU_EDITOR"] },
    { to: "/admin/items", label: "Ürünler", roles: ["RESTAURANT_OWNER", "BRANCH_MANAGER", "MENU_EDITOR"] },
    { to: "/admin/languages", label: "Diller", roles: ["RESTAURANT_OWNER", "BRANCH_MANAGER"] },
    { to: "/admin/restaurant", label: "Restoran Ayarlarý", roles: ["RESTAURANT_OWNER", "BRANCH_MANAGER"] }
  ].filter((item) => item.roles.some((role) => roles.includes(role)));

  return (
    <div className="admin-shell">
      <aside className="admin-sidebar">
        <div>
          <p className="eyebrow">Multi Language QR Menu</p>
          <h1>{session?.user.restaurantName}</h1>
          <p className="muted">Sprint 2-3 ile kategori ve ürün akýþý yönetilebilir hale geldi.</p>
        </div>
        <nav className="admin-nav">
          {menuItems.map((item) => <NavLink key={item.to} to={item.to} end={item.to === "/admin"} className={({ isActive }) => isActive ? "nav-link active" : "nav-link"}>{item.label}</NavLink>)}
        </nav>
        <button type="button" className="secondary-button" onClick={() => void logout()}>Çýkýþ Yap</button>
      </aside>
      <main className="admin-main">
        <header className="admin-header">
          <div>
            <p className="eyebrow">Aktif kullanýcý</p>
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
    request<HealthPayload>("/health", { method: "GET" }).then(setHealth).catch((exception) => setError(exception instanceof Error ? exception.message : "API durumu alýnamadý."));
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
        <p className="muted">Kategori CRUD, ürün CRUD, sýralama ve çok dilli alanlar ayný panelde çalýþýyor.</p>
      </article>
      <article className="panel-card"><p className="eyebrow">API Durumu</p><h3>{health?.status ?? "Kontrol ediliyor"}</h3><p className="muted">{health?.serverTimeUtc ?? (error || "Backend baðlantýsý test ediliyor.")}</p></article>
      <article className="panel-card"><p className="eyebrow">Kategori Sayýsý</p><h3>{categories.length}</h3><p className="muted">Aktif ve pasif tüm menü kategorileri burada sayýlýr.</p></article>
      <article className="panel-card"><p className="eyebrow">Ürün Sayýsý</p><h3>{items.length}</h3><p className="muted">Kategori filtreli ürün yönetimi ürün ekranýndan yapýlabilir.</p></article>
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
    request<RestaurantSettings>("/api/restaurants/current", { method: "GET" }, session.accessToken).then(setForm).catch((exception) => setError(exception instanceof Error ? exception.message : "Restoran verisi alýnamadý."));
  }, [session]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!session || !form) return;
    setSaving(true);
    setMessage("");
    setError("");
    try {
      await request<RestaurantSettings>("/api/restaurants/current", { method: "PUT", body: JSON.stringify(form) }, session.accessToken);
      setMessage("Restoran ayarlarý kaydedildi.");
      await refreshProfile();
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Kaydetme baþarýsýz oldu.");
    } finally {
      setSaving(false);
    }
  }

  if (!form) return <section className="panel-card">Restoran ayarlarý yükleniyor...</section>;

  return (
    <section className="panel-card">
      <div className="section-heading"><div><p className="eyebrow">Restoran Profili</p><h3>Ýþletme bilgilerini güncelle</h3></div><span className={form.isActive ? "status-pill success" : "status-pill muted"}>{form.isActive ? "Aktif" : "Pasif"}</span></div>
      <form className="settings-form" onSubmit={(event) => void handleSubmit(event)}>
        <label>Restoran adý<input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} /></label>
        <label>Telefon<input value={form.phone} onChange={(event) => setForm({ ...form, phone: event.target.value })} /></label>
        <label>WhatsApp<input value={form.whatsappPhone} onChange={(event) => setForm({ ...form, whatsappPhone: event.target.value })} /></label>
        <label>E-posta<input value={form.email} onChange={(event) => setForm({ ...form, email: event.target.value })} /></label>
        <label>Adres<textarea value={form.address} onChange={(event) => setForm({ ...form, address: event.target.value })} /></label>
        <label>Logo URL<input value={form.logoUrl} onChange={(event) => setForm({ ...form, logoUrl: event.target.value })} /></label>
        <label>Varsayýlan dil<input value={form.defaultLanguage} onChange={(event) => setForm({ ...form, defaultLanguage: event.target.value.toLowerCase() })} /></label>
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
      setError(exception instanceof Error ? exception.message : "Kategori listesi alýnamadý.");
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
    if (!session || !window.confirm("Bu kategoriyi silmek istediðine emin misin?")) return;
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
      setError(exception instanceof Error ? exception.message : "Kategori sýralamasý güncellenemedi.");
    }
  }

  return (
    <section className="management-shell">
      <div className="management-list panel-card">
        <div className="section-heading"><div><p className="eyebrow">Sprint 2</p><h3>Kategori Yönetimi</h3></div><button type="button" className="secondary-button" onClick={() => setForm(createEmptyCategoryForm())}>Yeni Kategori</button></div>
        {loading ? <p className="muted">Kategoriler yükleniyor...</p> : null}
        {categories.map((category, index) => <article key={category.id} className={form.id === category.id ? "list-card active" : "list-card"}><div><h4>{category.name}</h4><p className="muted">{category.description || "Açýklama yok"}</p><div className="inline-badges"><span className="role-badge">Sýra #{category.sortOrder}</span><span className={category.isActive ? "status-pill success" : "status-pill muted"}>{category.isActive ? "Aktif" : "Pasif"}</span><span className="role-badge">{category.itemCount} ürün</span></div></div><div className="row-actions"><button type="button" className="tiny-button" onClick={() => void reorderCategory(index, -1)}>Yukarý</button><button type="button" className="tiny-button" onClick={() => void reorderCategory(index, 1)}>Aþaðý</button><button type="button" className="tiny-button" onClick={() => setForm(mapCategoryToForm(category))}>Düzenle</button><button type="button" className="tiny-button danger" onClick={() => void removeCategory(category.id)}>Sil</button></div></article>)}
      </div>
      <div className="panel-card">
        <div className="section-heading"><div><p className="eyebrow">Kategori Formu</p><h3>{form.id ? "Kategoriyi düzenle" : "Yeni kategori oluþtur"}</h3></div><span className="role-badge">TR / EN / DE / RU</span></div>
        <form className="settings-form" onSubmit={(event) => void handleSubmit(event)}>
          <label>Kategori adý<input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} /></label>
          <label>Slug<input value={form.slug} onChange={(event) => setForm({ ...form, slug: event.target.value })} /></label>
          <label>Açýklama<textarea value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} /></label>
          <label>Görsel URL<input value={form.imageUrl} onChange={(event) => setForm({ ...form, imageUrl: event.target.value })} /></label>
          <label className="checkbox-row"><input type="checkbox" checked={form.isActive} onChange={(event) => setForm({ ...form, isActive: event.target.checked })} />Kategori aktif</label>
          <div className="translation-grid">{form.translations.map((translation, index) => <div key={translation.languageCode} className="translation-card"><p className="eyebrow">{translation.languageCode.toUpperCase()}</p><label>Ad<input value={translation.name} onChange={(event) => { const next = [...form.translations]; next[index] = { ...translation, name: event.target.value }; setForm({ ...form, translations: next }); }} /></label><label>Açýklama<textarea value={translation.description} onChange={(event) => { const next = [...form.translations]; next[index] = { ...translation, description: event.target.value }; setForm({ ...form, translations: next }); }} /></label></div>)}</div>
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
      setError(exception instanceof Error ? exception.message : "Ürün verileri alýnamadý.");
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
    if (!session || !window.confirm("Bu ürünü silmek istediðine emin misin?")) return;
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
      setError(exception instanceof Error ? exception.message : "Ürün sýralamasý güncellenemedi.");
    }
  }

  return (
    <section className="management-shell">
      <div className="management-list panel-card">
        <div className="section-heading"><div><p className="eyebrow">Sprint 3</p><h3>Ürün Yönetimi</h3></div><button type="button" className="secondary-button" onClick={() => setForm(createEmptyItemForm(selectedCategoryId || categories[0]?.id || ""))}>Yeni Ürün</button></div>
        <label>Kategori filtresi<select value={selectedCategoryId} onChange={(event) => setSelectedCategoryId(event.target.value)}><option value="">Tüm kategoriler</option>{categories.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}</select></label>
        {loading ? <p className="muted">Ürünler yükleniyor...</p> : null}
        {items.map((item) => <article key={item.id} className={form.id === item.id ? "list-card active" : "list-card"}><div><h4>{item.name}</h4><p className="muted">{item.categoryName} · {item.price} {item.currency}</p><div className="inline-badges"><span className={item.isAvailable ? "status-pill success" : "status-pill muted"}>{item.isAvailable ? "Stokta" : "Tükendi"}</span><span className={item.isActive ? "status-pill success" : "status-pill muted"}>{item.isActive ? "Aktif" : "Pasif"}</span>{item.isFeatured ? <span className="role-badge">Öne Çýkan</span> : null}</div></div><div className="row-actions"><button type="button" className="tiny-button" onClick={() => void reorderItem(item, -1)}>Yukarý</button><button type="button" className="tiny-button" onClick={() => void reorderItem(item, 1)}>Aþaðý</button><button type="button" className="tiny-button" onClick={() => setForm(mapItemToForm(item))}>Düzenle</button><button type="button" className="tiny-button danger" onClick={() => void removeItem(item.id)}>Sil</button></div></article>)}
      </div>
      <div className="panel-card">
        <div className="section-heading"><div><p className="eyebrow">Ürün Formu</p><h3>{form.id ? "Ürünü düzenle" : "Yeni ürün oluþtur"}</h3></div><span className="role-badge">Fiyat · Etiket · Çeviri</span></div>
        <form className="settings-form" onSubmit={(event) => void handleSubmit(event)}>
          <div className="two-column-grid"><label>Kategori<select value={form.categoryId} onChange={(event) => setForm({ ...form, categoryId: event.target.value })}>{categories.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}</select></label><label>Ürün adý<input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} /></label></div>
          <div className="two-column-grid"><label>Slug<input value={form.slug} onChange={(event) => setForm({ ...form, slug: event.target.value })} /></label><label>Görsel URL<input value={form.imageUrl} onChange={(event) => setForm({ ...form, imageUrl: event.target.value })} /></label></div>
          <label>Açýklama<textarea value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} /></label>
          <div className="two-column-grid"><label>Fiyat<input value={form.price} onChange={(event) => setForm({ ...form, price: event.target.value })} /></label><label>Ýndirimli fiyat<input value={form.discountedPrice} onChange={(event) => setForm({ ...form, discountedPrice: event.target.value })} /></label></div>
          <div className="two-column-grid"><label>Para birimi<input value={form.currency} onChange={(event) => setForm({ ...form, currency: event.target.value.toUpperCase() })} /></label><label>Baharat seviyesi (0-5)<input value={form.spiceLevel} onChange={(event) => setForm({ ...form, spiceLevel: event.target.value })} /></label></div>
          <div className="two-column-grid"><label>Hazýrlýk süresi<input value={form.preparationTimeMinutes} onChange={(event) => setForm({ ...form, preparationTimeMinutes: event.target.value })} /></label><label>Kalori<input value={form.calories} onChange={(event) => setForm({ ...form, calories: event.target.value })} /></label></div>
          <div className="checkbox-grid"><label className="checkbox-row"><input type="checkbox" checked={form.isVegetarian} onChange={(event) => setForm({ ...form, isVegetarian: event.target.checked })} />Vejetaryen</label><label className="checkbox-row"><input type="checkbox" checked={form.isVegan} onChange={(event) => setForm({ ...form, isVegan: event.target.checked })} />Vegan</label><label className="checkbox-row"><input type="checkbox" checked={form.isGlutenFree} onChange={(event) => setForm({ ...form, isGlutenFree: event.target.checked })} />Glutensiz</label><label className="checkbox-row"><input type="checkbox" checked={form.isFeatured} onChange={(event) => setForm({ ...form, isFeatured: event.target.checked })} />Öne çýkan</label><label className="checkbox-row"><input type="checkbox" checked={form.isAvailable} onChange={(event) => setForm({ ...form, isAvailable: event.target.checked })} />Stokta</label><label className="checkbox-row"><input type="checkbox" checked={form.isActive} onChange={(event) => setForm({ ...form, isActive: event.target.checked })} />Yayýnda</label></div>
          <div className="translation-grid">{form.translations.map((translation, index) => <div key={translation.languageCode} className="translation-card"><p className="eyebrow">{translation.languageCode.toUpperCase()}</p><label>Ad<input value={translation.name} onChange={(event) => { const next = [...form.translations]; next[index] = { ...translation, name: event.target.value }; setForm({ ...form, translations: next }); }} /></label><label>Açýklama<textarea value={translation.description} onChange={(event) => { const next = [...form.translations]; next[index] = { ...translation, description: event.target.value }; setForm({ ...form, translations: next }); }} /></label></div>)}</div>
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
      setError(exception instanceof Error ? exception.message : "Dil ayarlari alinamadi.");
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
      setMessage("Restoran dil ayarlari guncellendi.");
      await refreshProfile();
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Dil ayarlari kaydedilemedi.");
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
            <h3>Public Dil Ayarlari</h3>
          </div>
          <span className="role-badge">TR / EN / DE / RU</span>
        </div>
        <p className="muted">Public menude gorunecek dilleri ve varsayilan dili buradan belirleyebilirsin.</p>
        <form className="settings-form" onSubmit={(event) => void handleSubmit(event)}>
          {loading ? <p className="muted">Dil ayarlari yukleniyor...</p> : null}
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
                    <label className="checkbox-row"><input type="radio" name="defaultLanguage" checked={defaultLanguage === language.code} disabled={!checked} onChange={() => setDefaultLanguage(language.code)} />Varsayilan</label>
                  </div>
                </label>
              );
            })}
          </div>
          {enabledCodes.length === 0 ? <p className="form-error">En az bir dil aktif kalmalidir.</p> : null}
          {message ? <p className="form-success">{message}</p> : null}
          {error ? <p className="form-error">{error}</p> : null}
          <button className="primary-button" type="submit" disabled={saving || enabledCodes.length === 0}>{saving ? "Kaydediliyor..." : "Dil Ayarlarini Kaydet"}</button>
        </form>
      </article>
      <article className="panel-card">
        <p className="eyebrow">Ceviri Kalitesi</p>
        <h3>Admin Panel Rehberi</h3>
        <p className="muted">Kategori ve urun formlarinda ceviri alanlari zaten aktif. Public menude eksik ceviri varsa sistem otomatik olarak restoranin varsayilan diline geri duser.</p>
        <div className="inline-badges">
          <span className="role-badge">Fallback destekli</span>
          <span className="role-badge">Turist dostu</span>
          <span className="role-badge">SaaS hazir</span>
        </div>
      </article>
    </section>
  );
}

function PublicLayout() {
  const { slug } = useParams();
  const [restaurant, setRestaurant] = useState<PublicRestaurant | null>(null);
  const [menu, setMenu] = useState<PublicMenuPayload | null>(null);
  const [language, setLanguage] = useState("");
  const [search, setSearch] = useState("");
  const deferredSearch = useDeferredValue(search);
  const [selectedCategoryId, setSelectedCategoryId] = useState("");
  const [selectedItem, setSelectedItem] = useState<PublicMenuItem | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!slug) return;
    request<PublicRestaurant>(`/public/restaurants/${slug}`, { method: "GET" })
      .then((data) => {
        setRestaurant(data);
        const saved = window.localStorage.getItem(`mlqm-public-lang-${slug}`) ?? "";
        const nextLanguage = data.activeLanguages.some((entry) => entry.code === saved) ? saved : data.selectedLanguage || data.defaultLanguage;
        setLanguage(nextLanguage);
      })
      .catch((exception) => setError(exception instanceof Error ? exception.message : "Menu yuklenemedi."));
  }, [slug]);

  useEffect(() => {
    if (!slug || !language) return;
    setLoading(true);
    request<PublicMenuPayload>(`/public/restaurants/${slug}/menu?lang=${language}&search=${encodeURIComponent(deferredSearch)}`, { method: "GET" })
      .then((data) => {
        setMenu(data);
        setRestaurant(data.restaurant);
        window.localStorage.setItem(`mlqm-public-lang-${slug}`, data.languageCode);
      })
      .catch((exception) => setError(exception instanceof Error ? exception.message : "Menu yuklenemedi."))
      .finally(() => setLoading(false));
  }, [slug, language, deferredSearch]);

  useEffect(() => {
    const firstCategoryId = menu?.categories[0]?.id ?? "";
    if (!selectedCategoryId || !menu?.categories.some((category) => category.id === selectedCategoryId)) {
      setSelectedCategoryId(firstCategoryId);
    }
  }, [menu, selectedCategoryId]);

  const visibleCategories = menu?.categories.filter((category) => !selectedCategoryId || category.id === selectedCategoryId) ?? [];
  const whatsappLink = restaurant?.whatsappPhone ? `https://wa.me/${normalizePhoneLink(restaurant.whatsappPhone)}` : "";
  const phoneLink = restaurant?.phone ? `tel:${normalizePhoneLink(restaurant.phone)}` : "";

  return (
    <div className="public-menu-shell">
      <section className="public-menu-page">
        <header className="public-topbar">
          <div>
            <p className="eyebrow">Public QR Menu</p>
            <h1>{restaurant?.name ?? slug}</h1>
            <p className="muted">{restaurant?.address || "Mobil hizli menu deneyimi"}</p>
          </div>
          <div className="inline-badges">
            {restaurant?.activeLanguages.map((entry) => <button key={entry.code} type="button" className={language === entry.code ? "tiny-button active-chip" : "tiny-button"} onClick={() => setLanguage(entry.code)}>{entry.code.toUpperCase()}</button>)}
          </div>
        </header>

        <section className="public-hero-card">
          <div>
            <p className="eyebrow">Bugunun Menusu</p>
            <h2>QR okut, ara, sec ve incele</h2>
            <p className="muted">Aktif kategori ve urunler secilen dilde aninda gorunur.</p>
          </div>
          <div className="cta-row">
            {whatsappLink ? <a className="primary-button" href={whatsappLink} target="_blank" rel="noreferrer">WhatsApp</a> : null}
            {phoneLink ? <a className="secondary-button" href={phoneLink}>Ara</a> : null}
          </div>
        </section>

        <label>
          Urun ara
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Orn. kahve, burger, tatli" />
        </label>

        <div className="category-strip">
          {menu?.categories.map((category) => <button key={category.id} type="button" className={selectedCategoryId === category.id ? "category-chip active-chip" : "category-chip"} onClick={() => setSelectedCategoryId(category.id)}>{category.name}</button>)}
        </div>

        {loading ? <section className="public-menu-card"><p className="muted">Menu yukleniyor...</p></section> : null}
        {error ? <section className="public-menu-card"><p className="form-error">{error}</p></section> : null}

        {visibleCategories.map((category) => (
          <section key={category.id} className="public-section-card">
            <div className="section-heading">
              <div>
                <p className="eyebrow">Kategori</p>
                <h3>{category.name}</h3>
              </div>
              <span className="role-badge">{category.items.length} urun</span>
            </div>
            {category.description ? <p className="muted">{category.description}</p> : null}
            <div className="public-item-grid">
              {category.items.map((item) => (
                <button key={item.id} type="button" className="public-item-card" onClick={() => setSelectedItem(item)}>
                  <div className="public-item-copy">
                    <div className="section-heading compact-row">
                      <strong>{item.name}</strong>
                      <span className={item.isAvailable ? "status-pill success" : "status-pill muted"}>{item.isAvailable ? "Serviste" : "Tukendi"}</span>
                    </div>
                    <p className="muted">{item.description}</p>
                    <div className="inline-badges">
                      {item.isFeatured ? <span className="role-badge">One Cikan</span> : null}
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
                <p className="eyebrow">Urun Detayi</p>
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



