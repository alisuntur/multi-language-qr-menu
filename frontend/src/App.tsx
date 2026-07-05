import {
  FormEvent,
  createContext,
  useContext,
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

type AuthContextValue = {
  session: AuthSession | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  refreshProfile: () => Promise<void>;
};

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5099";
const STORAGE_KEY = "mlqm-session";
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
          // Ignore logout API failures and clear local session.
        }
      }

      setSession(null);
    },
    async refreshProfile() {
      if (!session) {
        return;
      }

      const user = await request<AuthUser>("/api/auth/me", { method: "GET" }, session.accessToken);
      setSession({ ...session, user });
    }
  }), [loading, session]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("Auth context is not available.");
  }

  return context;
}

function ProtectedRoute() {
  const { session } = useAuth();
  const location = useLocation();

  if (!session) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }

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
          <h1>QR menünü dakikalar içinde yönet</h1>
          <p className="muted">Sprint 0 ve Sprint 1 için giriş, panel ve restoran ayarları hazır.</p>
        </div>

        <form className="auth-form" onSubmit={(event) => void handleSubmit(event)}>
          <label>
            E-posta
            <input value={email} onChange={(event) => setEmail(event.target.value)} type="email" />
          </label>
          <label>
            Şifre
            <input value={password} onChange={(event) => setPassword(event.target.value)} type="password" />
          </label>
          {error ? <p className="form-error">{error}</p> : null}
          <button type="submit" className="primary-button" disabled={loading}>
            {loading ? "Giriş yapılıyor..." : "Giriş Yap"}
          </button>
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
    { to: "/admin/restaurant", label: "Restoran Ayarları", roles: ["RESTAURANT_OWNER", "BRANCH_MANAGER"] }
  ].filter((item) => item.roles.some((role) => roles.includes(role)));

  return (
    <div className="admin-shell">
      <aside className="admin-sidebar">
        <div>
          <p className="eyebrow">Multi Language QR Menu</p>
          <h1>{session?.user.restaurantName}</h1>
          <p className="muted">Anlık fiyat yönetimi, tenant izolasyonu ve rol bazlı erişim hazır.</p>
        </div>

        <nav className="admin-nav">
          {menuItems.map((item) => (
            <NavLink key={item.to} to={item.to} end={item.to === "/admin"} className={({ isActive }) => isActive ? "nav-link active" : "nav-link"}>
              {item.label}
            </NavLink>
          ))}
        </nav>

        <button type="button" className="secondary-button" onClick={() => void logout()}>
          Çıkış Yap
        </button>
      </aside>

      <main className="admin-main">
        <header className="admin-header">
          <div>
            <p className="eyebrow">Aktif kullanıcı</p>
            <h2>{session?.user.fullName}</h2>
          </div>
          <div className="badge-row">
            {roles.map((role) => (
              <span key={role} className="role-badge">{role}</span>
            ))}
          </div>
        </header>
        <Outlet />
      </main>
    </div>
  );
}

function DashboardPage() {
  const { session } = useAuth();
  const [health, setHealth] = useState<HealthPayload | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    request<HealthPayload>("/health", { method: "GET" })
      .then(setHealth)
      .catch((exception) => setError(exception instanceof Error ? exception.message : "API durumu alınamadı."));
  }, []);

  return (
    <section className="content-grid">
      <article className="panel-card hero-card">
        <p className="eyebrow">Sprint 1 durumu</p>
        <h3>{session?.user.restaurantName}</h3>
        <p className="muted">JWT auth, refresh token, restoran profili ve rol bazlı menü akışı canlı.</p>
      </article>

      <article className="panel-card">
        <p className="eyebrow">API Durumu</p>
        <h3>{health?.status ?? "Kontrol ediliyor"}</h3>
        <p className="muted">{health?.serverTimeUtc ?? (error || "Backend bağlantısı test ediliyor.")}</p>
      </article>

      <article className="panel-card">
        <p className="eyebrow">Aktif Roller</p>
        <h3>{session?.user.roles.join(", ")}</h3>
        <p className="muted">Görünür menü elemanları kullanıcının rolüne göre filtrelenir.</p>
      </article>

      <article className="panel-card">
        <p className="eyebrow">Public Menü Rotası</p>
        <h3>/menu/{session?.user.restaurantSlug}</h3>
        <p className="muted">Public mobil layout iskeleti Sprint 0 kapsamında hazırlandı.</p>
      </article>
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
    if (!session) {
      return;
    }

    request<RestaurantSettings>("/api/restaurants/current", { method: "GET" }, session.accessToken)
      .then(setForm)
      .catch((exception) => setError(exception instanceof Error ? exception.message : "Restoran verisi alınamadı."));
  }, [session]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!session || !form) {
      return;
    }

    setSaving(true);
    setMessage("");
    setError("");

    try {
      await request<RestaurantSettings>("/api/restaurants/current", {
        method: "PUT",
        body: JSON.stringify(form)
      }, session.accessToken);
      setMessage("Restoran ayarları kaydedildi.");
      await refreshProfile();
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Kaydetme başarısız oldu.");
    } finally {
      setSaving(false);
    }
  }

  if (!form) {
    return <section className="panel-card">Restoran ayarları yükleniyor...</section>;
  }

  return (
    <section className="panel-card">
      <div className="section-heading">
        <div>
          <p className="eyebrow">Restoran Profili</p>
          <h3>İşletme bilgilerini güncelle</h3>
        </div>
        <span className={form.isActive ? "status-pill success" : "status-pill muted"}>{form.isActive ? "Aktif" : "Pasif"}</span>
      </div>

      <form className="settings-form" onSubmit={(event) => void handleSubmit(event)}>
        <label>
          Restoran adı
          <input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} />
        </label>
        <label>
          Telefon
          <input value={form.phone} onChange={(event) => setForm({ ...form, phone: event.target.value })} />
        </label>
        <label>
          WhatsApp
          <input value={form.whatsappPhone} onChange={(event) => setForm({ ...form, whatsappPhone: event.target.value })} />
        </label>
        <label>
          E-posta
          <input value={form.email} onChange={(event) => setForm({ ...form, email: event.target.value })} />
        </label>
        <label>
          Adres
          <textarea value={form.address} onChange={(event) => setForm({ ...form, address: event.target.value })} />
        </label>
        <label>
          Logo URL
          <input value={form.logoUrl} onChange={(event) => setForm({ ...form, logoUrl: event.target.value })} />
        </label>
        <label>
          Varsayılan dil
          <input value={form.defaultLanguage} onChange={(event) => setForm({ ...form, defaultLanguage: event.target.value.toLowerCase() })} />
        </label>
        <label>
          Para birimi
          <input value={form.currency} onChange={(event) => setForm({ ...form, currency: event.target.value.toUpperCase() })} />
        </label>

        {message ? <p className="form-success">{message}</p> : null}
        {error ? <p className="form-error">{error}</p> : null}

        <button className="primary-button" type="submit" disabled={saving}>
          {saving ? "Kaydediliyor..." : "Kaydet"}
        </button>
      </form>
    </section>
  );
}

function PublicLayout() {
  const { slug } = useParams();

  return (
    <div className="public-shell">
      <header className="public-hero">
        <p className="eyebrow">QR Menu Demo</p>
        <h1>{slug}</h1>
        <p className="muted">Mobil public layout iskeleti, kapak alanı ve menü container hazırlığı tamam.</p>
      </header>

      <section className="public-menu-card">
        <div className="section-heading">
          <div>
            <p className="eyebrow">Public Layout</p>
            <h2>QR Menü Mobil İskeleti</h2>
          </div>
          <span className="status-pill success">Hazır</span>
        </div>

        <p className="muted">Sprint 4 öncesi restoran kapak görseli, dil seçici ve kategori çubuğu için temel yapı hazır.</p>

        <div className="placeholder-stack">
          <div className="placeholder-line" />
          <div className="placeholder-line short" />
          <div className="placeholder-line" />
        </div>
      </section>
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
            <Route path="restaurant" element={<RestaurantSettingsPage />} />
          </Route>
        </Route>
        <Route path="/menu/:slug" element={<PublicLayout />} />
      </Routes>
    </AuthProvider>
  );
}

