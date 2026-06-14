// ══════════════════════════════════════════
// CONFIGURACIÓN DE LA API
// ══════════════════════════════════════════
// En producción (Railway), el frontend y el backend están en el mismo servidor,
// por lo que usamos rutas relativas. En desarrollo local, apuntamos a localhost.
const API_URL = "/api";

let DB = { users: [], products: [], customers: [], suppliers: [], purchases: [], audit: [], queue: [], sales: [] };
let session = null;
let isOnline = true;
let cart = [];
let purchaseCart = [];
let selectedPayMethod = 'Efectivo';
let saleCounter = 0;
let purchaseCounter = 0;

// Configuración de encabezados para peticiones con JWT
function getHeaders() {
  const token = localStorage.getItem('sp_token');
  return {
    'Content-Type': 'application/json',
    'Authorization': token ? `Bearer ${token}` : ''
  };
}

// ══════════════════════════════════════════
// DB / PERSISTENCIA - LLAMADAS A LA API
// ══════════════════════════════════════════
async function initDB() {
  const token = localStorage.getItem('sp_token');
  const storedUser = localStorage.getItem('sp_user');
  
  if (token && storedUser) {
    try {
      session = JSON.parse(storedUser);
      document.getElementById('s-name').textContent = session.FullName;
      document.getElementById('s-role').textContent = session.RoleName || session.Role;
      document.getElementById('f-user').textContent = session.Username + ' (' + (session.RoleName || session.Role) + ')';
      document.getElementById('login-overlay').style.display = 'none';
      document.getElementById('app-shell').style.display = 'grid';
      await refreshAllData();
      nav('Dashboard');
    } catch (e) {
      console.error("Token expirado o inválido", e);
      logout();
    }
  } else {
    logout();
  }
}

async function refreshAllData() {
  showMsg("Cargando datos en tiempo real desde la nube...");
  try {
    const productsRes = await fetch(`${API_URL}/products`, { headers: getHeaders() });
    if (productsRes.ok) DB.products = await productsRes.json();

    const customersRes = await fetch(`${API_URL}/customers`, { headers: getHeaders() });
    if (customersRes.ok) DB.customers = await customersRes.json();

    const suppliersRes = await fetch(`${API_URL}/suppliers`, { headers: getHeaders() });
    if (suppliersRes.ok) DB.suppliers = await suppliersRes.json();

    const logsRes = await fetch(`${API_URL}/auditlogs`, { headers: getHeaders() });
    if (logsRes.ok) DB.audit = await logsRes.json();

    // Las ventas y compras se simulan localmente o se leen del historial de auditoría
    // Para simplificar, inicializamos arrays vacíos si el endpoint no existe
    DB.sales = DB.sales || [];
    DB.purchases = DB.purchases || [];

    updateOutbox();
    showMsg("✅ Base de datos al día con Firestore.");
  } catch (err) {
    console.error("Error al refrescar datos", err);
    toast("Error de conexión con la API", "err");
    showMsg("⚠️ Modo offline: cargando datos locales en caché.");
  }
}

function updateOutbox() {
  const n = (DB.queue || []).length;
  document.getElementById('ob-n').textContent = n;
}

function showMsg(msg) {
  const el = document.getElementById('sync-msg');
  if (el) el.textContent = msg;
}

function toggleNet() {
  isOnline = !isOnline;
  const pill = document.getElementById('net-pill');
  const txt = document.getElementById('net-txt');
  if (isOnline) {
    pill.classList.remove('offline');
    txt.textContent = 'Online';
    showMsg('Conectado. Sincronizando datos...');
    refreshAllData();
  } else {
    pill.classList.add('offline');
    txt.textContent = 'Offline';
    showMsg('Modo offline activo localmente.');
  }
}

function syncNow() {
  if (!isOnline) {
    toast('Sin conexión. Activa Online para sincronizar.', 'err');
    return;
  }
  refreshAllData();
  toast('✅ Datos sincronizados con Firestore', 'ok');
}

// ══════════════════════════════════════════
// TOAST
// ══════════════════════════════════════════
function toast(msg, type = 'ok') {
  const el = document.getElementById('toast');
  el.textContent = msg;
  el.className = 'show toast-' + type;
  clearTimeout(el._t);
  el._t = setTimeout(() => el.className = '', 2800);
}

// ══════════════════════════════════════════
// AUDITORÍA
// ══════════════════════════════════════════
async function audit(userId, username, type, desc) {
  const log = {
    Id: 'a_' + Date.now(),
    UserId: userId || '?',
    Username: username || 'System',
    EventTime: new Date().toISOString(),
    EventType: type,
    MachineName: 'WEB-CLIENT',
    Description: desc
  };
  
  DB.audit.unshift(log);
  if (document.getElementById('t-audit')) renderAudit();

  try {
    await fetch(`${API_URL}/auditlogs`, {
      method: 'POST',
      headers: getHeaders(),
      body: JSON.stringify(log)
    });
  } catch (e) {
    console.error("Error al registrar auditoría en el servidor", e);
  }
}

// ══════════════════════════════════════════
// AUTENTICACIÓN
// ══════════════════════════════════════════
async function login() {
  const u = document.getElementById('l-user').value.trim().toLowerCase();
  const p = document.getElementById('l-pass').value;
  const err = document.getElementById('l-err');
  err.textContent = '';

  if (!u || !p) {
    err.textContent = 'Ingresa usuario y contraseña.';
    return;
  }

  try {
    const res = await fetch(`${API_URL}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username: u, password: p })
    });

    if (!res.ok) {
      const data = await res.json();
      err.textContent = data.message || 'Error de autenticación.';
      return;
    }

    const data = await res.json();
    localStorage.setItem('sp_token', data.token);
    localStorage.setItem('sp_user', JSON.stringify(data.user));
    
    session = data.user;
    document.getElementById('s-name').textContent = session.FullName;
    document.getElementById('s-role').textContent = session.RoleName || session.Role;
    document.getElementById('f-user').textContent = session.Username + ' (' + (session.RoleName || session.Role) + ')';
    document.getElementById('login-overlay').style.display = 'none';
    document.getElementById('app-shell').style.display = 'grid';
    document.getElementById('l-user').value = '';
    document.getElementById('l-pass').value = '';

    await refreshAllData();
    nav('Dashboard');
  } catch (e) {
    console.error(e);
    err.textContent = 'No se pudo conectar con el servidor API.';
  }
}

function logout() {
  if (session) audit(session.Id, session.Username, 'Logout', 'Sesión cerrada por el usuario.');
  localStorage.removeItem('sp_token');
  localStorage.removeItem('sp_user');
  session = null;
  cart = [];
  document.getElementById('app-shell').style.display = 'none';
  document.getElementById('login-overlay').style.display = 'flex';
}

// ══════════════════════════════════════════
// NAVEGACIÓN
// ══════════════════════════════════════════
const TAB_TITLES = {
  Dashboard: 'Dashboard', Ventas: 'Nueva Venta', HistorialVentas: 'Historial de Ventas',
  Compras: 'Entrada / Compra de Inventario', HistorialCompras: 'Historial de Compras',
  Inventario: 'Catálogo e Inventario', Productos: 'Gestión de Productos (CRUD)',
  CRM: 'Clientes y CRM', Proveedores: 'Gestión de Proveedores (CRUD)',
  Reportes: 'Reportes de Inventario y Ventas',
  Usuarios: 'Gestión de Usuarios', Auditoria: 'Auditoría de Accesos'
};

function nav(tab) {
  const isAdm = session && (session.RoleName === 'Administrador' || session.Role === 0);
  if ((tab === 'Usuarios' || tab === 'Auditoria') && !isAdm) {
    audit(session.Id, session.Username, 'Access_Denied', `Intento no autorizado: panel de ${tab}.`);
    toast(`⛔ Acceso denegado. Se requiere rol Administrador.`, 'err');
    return;
  }
  document.querySelectorAll('.menu-item').forEach(b => b.classList.remove('active'));
  document.getElementById('m-' + tab)?.classList.add('active');
  document.querySelectorAll('.panel').forEach(p => p.classList.remove('active'));
  document.getElementById('p-' + tab).classList.add('active');
  document.getElementById('tab-title').textContent = TAB_TITLES[tab] || tab;

  if (tab === 'Inventario') renderInventory();
  if (tab === 'Productos') { renderProductos(); clearProductoForm(); }
  if (tab === 'CRM') { renderCRM(); clearCRMForm(); }
  if (tab === 'Proveedores') { renderProveedores(); clearProveedorForm(); }
  if (tab === 'Dashboard') { refreshDashboard(); }
  if (tab === 'Auditoria') { document.getElementById('audit-denied').style.display = 'none'; document.getElementById('audit-cont').style.display = 'block'; renderAudit(); }
  if (tab === 'Usuarios') { document.getElementById('usr-denied').style.display = 'none'; document.getElementById('usr-cont').style.display = 'grid'; renderUsers(); }
  if (tab === 'Ventas') { initSalesPanel(); }
  if (tab === 'Compras') { initComprasPanel(); }
  if (tab === 'HistorialVentas') { renderHistory(); }
  if (tab === 'HistorialCompras') { renderHistorialCompras(); }
  if (tab === 'Reportes') { renderReportes(); }
}

// ══════════════════════════════════════════
// REPORTES
// ══════════════════════════════════════════
function showRepTab(tabId) {
  ['critico', 'top', 'slow'].forEach(id => {
    document.getElementById('rep-tab-' + id).classList.remove('btn');
    document.getElementById('rep-tab-' + id).classList.add('btn-secondary');
    document.getElementById('rep-sec-' + id).style.display = 'none';
  });
  const btn = document.getElementById('rep-tab-' + tabId);
  btn.classList.remove('btn-secondary');
  btn.classList.add('btn');
  document.getElementById('rep-sec-' + tabId).style.display = 'block';
}

function renderReportes() {
  const days = parseInt(document.getElementById('rep-days').value) || 0;
  const msThreshold = days > 0 ? (days * 24 * 60 * 60 * 1000) : 0;
  const now = new Date().getTime();

  const criticos = DB.products.filter(p => (p.WarehouseMain + p.WarehouseSecondary) <= p.MinimumStock)
    .sort((a, b) => (a.WarehouseMain + a.WarehouseSecondary) - (b.WarehouseMain + b.WarehouseSecondary));
  document.getElementById('rep-kpi-critico').textContent = criticos.length;
  document.getElementById('rep-critico-empty').style.display = criticos.length === 0 ? 'block' : 'none';
  document.getElementById('t-rep-critico').innerHTML = criticos.map(p => {
    const stock = p.WarehouseMain + p.WarehouseSecondary;
    const deficit = p.MinimumStock - stock;
    const alertHtml = stock === 0 ? '<span class="tag tag-low">Agotado</span>' : '<span class="tag tag-warning" style="background:var(--warning);color:#000">Crítico</span>';
    return `<tr>
      <td><b>${p.Name}</b></td><td>${p.SKU || p.EAN}</td><td>${p.Category || '—'}</td>
      <td style="font-weight:bold;color:${stock === 0 ? 'var(--danger)' : 'var(--warning)'}">${stock} u.</td>
      <td>${p.MinimumStock} u.</td><td style="color:var(--danger)">-${deficit} u.</td>
      <td>${alertHtml}</td>
    </tr>`;
  }).join('');

  const relevantSales = DB.sales.filter(s => msThreshold === 0 || (now - new Date(s.Date).getTime() <= msThreshold));
  const productSales = {};
  relevantSales.forEach(s => {
    s.Lines.forEach(l => {
      if (!productSales[l.ProductId]) productSales[l.ProductId] = { qty: 0, revenue: 0, name: l.Name, sku: l.SKU, category: '' };
      productSales[l.ProductId].qty += l.Qty;
      productSales[l.ProductId].revenue += l.Subtotal;
    });
  });
  DB.products.forEach(p => {
    if (productSales[p.Id]) productSales[p.Id].category = p.Category || '—';
  });

  const totalRevenue = Object.values(productSales).reduce((sum, s) => sum + s.revenue, 0);

  const topProducts = Object.values(productSales).sort((a, b) => b.qty - a.qty);
  if (topProducts.length > 0) {
    document.getElementById('rep-kpi-top').textContent = topProducts[0].name.substring(0, 20) + (topProducts[0].name.length > 20 ? '...' : '');
    document.getElementById('rep-kpi-top-qty').textContent = `${topProducts[0].qty} u. vendidas`;
  } else {
    document.getElementById('rep-kpi-top').textContent = '—';
    document.getElementById('rep-kpi-top-qty').textContent = '';
  }
  document.getElementById('rep-top-empty').style.display = topProducts.length === 0 ? 'block' : 'none';
  document.getElementById('t-rep-top').innerHTML = topProducts.map((p, i) => {
    const perc = totalRevenue > 0 ? ((p.revenue / totalRevenue) * 100).toFixed(1) : 0;
    return `<tr>
      <td>#${i + 1}</td><td><b>${p.name}</b></td><td>${p.sku}</td><td>${p.category}</td>
      <td style="font-weight:bold;color:var(--accent)">${p.qty}</td>
      <td>$${p.revenue.toLocaleString('es-CO', { minimumFractionDigits: 2 })}</td>
      <td>
        <div style="display:flex;align-items:center;gap:6px">
          <div style="width:50px;height:6px;background:#333;border-radius:3px;overflow:hidden">
            <div style="height:100%;width:${perc}%;background:#2ecc71"></div>
          </div>
          <span style="font-size:10px">${perc}%</span>
        </div>
      </td>
    </tr>`;
  }).join('');

  const slowProducts = DB.products.filter(p => !productSales[p.Id]);
  document.getElementById('rep-kpi-slow').textContent = slowProducts.length;
  document.getElementById('rep-kpi-total').textContent = DB.products.length;
  document.getElementById('rep-slow-empty').style.display = slowProducts.length === 0 ? 'block' : 'none';
  document.getElementById('t-rep-slow').innerHTML = slowProducts.map(p => {
    const stock = p.WarehouseMain + p.WarehouseSecondary;
    return `<tr>
      <td><b>${p.Name}</b></td><td>${p.SKU}</td><td>${p.Category || '—'}</td>
      <td>${stock} u.</td><td>$${p.Price.toLocaleString('es-CO')}</td>
      <td>$${(stock * p.Cost).toLocaleString('es-CO')}</td>
      <td style="color:var(--warning)">Sin movimiento</td>
    </tr>`;
  }).join('');
}

// ══════════════════════════════════════════
// DASHBOARD
// ══════════════════════════════════════════
function refreshDashboard() {
  let totalVal = 0;
  let alertsCount = 0;
  DB.products.forEach(p => {
    const stock = p.WarehouseMain + p.WarehouseSecondary;
    totalVal += stock * p.Cost;
    if (stock <= p.MinimumStock) alertsCount++;
  });

  document.getElementById('kpi-inv').textContent = '$' + totalVal.toLocaleString('es-CO', { maximumFractionDigits: 0 });
  document.getElementById('kpi-inv-sub').textContent = `${DB.products.length} productos en catálogo`;

  let salesToday = 0;
  let salesCount = 0;
  DB.sales.forEach(s => {
    salesToday += s.Total;
    salesCount++;
  });

  document.getElementById('kpi-sales').textContent = '$' + salesToday.toLocaleString('es-CO', { maximumFractionDigits: 0 });
  document.getElementById('kpi-sales-sub').textContent = `${salesCount} transacciones registradas`;

  const activeCust = DB.customers.filter(c => c.IsActive).length;
  document.getElementById('kpi-cust').textContent = activeCust;

  document.getElementById('kpi-alerts').textContent = alertsCount;
  document.getElementById('kpi-alerts-sub').textContent = alertsCount > 0 ? "Requiere reabastecimiento" : "Niveles de stock estables";

  const tb = document.getElementById('t-dash-sales');
  tb.innerHTML = '';
  const latestSales = [...DB.sales].reverse().slice(0, 5);
  if (!latestSales.length) {
    tb.innerHTML = '<tr><td colspan="6" style="text-align:center;color:var(--text-dim);padding:10px">Sin ventas registradas.</td></tr>';
  } else {
    latestSales.forEach(s => {
      const d = new Date(s.Date);
      const timeStr = d.toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit' });
      const tr = document.createElement('tr');
      tr.innerHTML = `<td><strong>#${s.SaleNumber}</strong></td>
        <td>${s.CustomerName || 'Consumidor Final'}</td>
        <td style="text-align:center">${s.Lines.length}</td>
        <td style="font-weight:bold;color:var(--success)">$${s.Total.toLocaleString('es-CO', { minimumFractionDigits: 0 })}</td>
        <td><span class="tag tag-pipeline">${s.PayMethod}</span></td>
        <td style="color:var(--text-dim)">${timeStr}</td>`;
      tb.appendChild(tr);
    });
  }

  const lowStockList = document.getElementById('low-stock-list');
  const lowStockItems = DB.products.filter(p => (p.WarehouseMain + p.WarehouseSecondary) <= p.MinimumStock).slice(0, 5);
  if (!lowStockItems.length) {
    lowStockList.innerHTML = '✅ Todo el inventario está en niveles óptimos.';
  } else {
    lowStockList.innerHTML = lowStockItems.map(p => {
      const total = p.WarehouseMain + p.WarehouseSecondary;
      return `⚠️ <strong>${p.Name}</strong> tiene ${total} u. (Min: ${p.MinimumStock})`;
    }).join('<br>');
  }
}

// ══════════════════════════════════════════
// INVENTARIO / CATALOGO
// ══════════════════════════════════════════
function renderInventory() {
  const tb = document.getElementById('t-inv');
  tb.innerHTML = '';
  DB.products.forEach(p => {
    const total = p.WarehouseMain + p.WarehouseSecondary;
    const tr = document.createElement('tr');
    const isLow = total <= p.MinimumStock;
    tr.innerHTML = `<td style="font-family:monospace">${p.EAN}</td>
      <td style="font-family:monospace">${p.SKU}</td>
      <td><strong>${p.Name}</strong></td>
      <td>${p.Category}</td>
      <td>$${p.Cost.toLocaleString('es-CO')}</td>
      <td>$${p.Price.toLocaleString('es-CO')}</td>
      <td style="text-align:center">${p.WarehouseMain}</td>
      <td style="text-align:center">${p.WarehouseSecondary}</td>
      <td style="text-align:center;font-weight:bold;color:${isLow ? 'var(--danger)' : 'inherit'}">${total} u.</td>
      <td style="text-align:center;color:var(--text-dim)">${p.MinimumStock} u.</td>
      <td><span class="tag ${isLow ? 'tag-low' : 'tag-ok'}">${isLow ? 'BAJO MÍNIMO' : 'ÓPTIMO'}</span></td>`;
    tb.appendChild(tr);
  });
}

// ══════════════════════════════════════════
// PRODUCTOS CRUD
// ══════════════════════════════════════════
function renderProductos() {
  const tb = document.getElementById('t-prod');
  const q = (document.getElementById('prod-search')?.value || '').toLowerCase();
  tb.innerHTML = '';
  const filtered = DB.products.filter(p => !q || p.Name.toLowerCase().includes(q) || p.SKU.toLowerCase().includes(q) || p.EAN.includes(q));
  
  filtered.forEach(p => {
    const tr = document.createElement('tr');
    const total = p.WarehouseMain + p.WarehouseSecondary;
    const activeId = document.getElementById('prod-id').value;
    if (activeId === p.Id) tr.classList.add('sel-row');
    tr.onclick = () => selectProducto(p);
    tr.innerHTML = `<td style="font-family:monospace">${p.EAN}</td>
      <td style="font-family:monospace"><strong>${p.SKU}</strong></td>
      <td>${p.Name}</td>
      <td>${p.Category}</td>
      <td>$${p.Cost.toLocaleString('es-CO')}</td>
      <td>$${p.Price.toLocaleString('es-CO')}</td>
      <td style="text-align:center;font-weight:bold">${total} u.</td>
      <td><span class="tag tag-ok">Activo</span></td>`;
    tb.appendChild(tr);
  });
}

function selectProducto(p) {
  document.getElementById('prod-id').value = p.Id;
  document.getElementById('prod-ean').value = p.EAN || '';
  document.getElementById('prod-sku').value = p.SKU;
  document.getElementById('prod-name').value = p.Name;
  document.getElementById('prod-cat').value = p.Category;
  document.getElementById('prod-cost').value = p.Cost;
  document.getElementById('prod-price').value = p.Price;
  document.getElementById('prod-wh-main').value = p.WarehouseMain;
  document.getElementById('prod-wh-sec').value = p.WarehouseSecondary;
  document.getElementById('prod-min').value = p.MinimumStock;
  document.getElementById('prod-form-title').textContent = 'Editar Producto';
  
  const btnDel = document.getElementById('btn-del-prod');
  btnDel.style.display = 'inline-flex';
  btnDel.innerHTML = '🗑️ Eliminar';
  btnDel.className = 'btn btn-sm btn-danger';
  
  updateProductoMargin();
  renderProductos();
}

function clearProductoForm() {
  document.getElementById('prod-id').value = '';
  document.getElementById('prod-ean').value = '';
  document.getElementById('prod-sku').value = '';
  document.getElementById('prod-name').value = '';
  document.getElementById('prod-cat').value = 'Tecnología';
  document.getElementById('prod-cost').value = '';
  document.getElementById('prod-price').value = '';
  document.getElementById('prod-wh-main').value = '0';
  document.getElementById('prod-wh-sec').value = '0';
  document.getElementById('prod-min').value = '5';
  document.getElementById('prod-form-title').textContent = 'Nuevo Producto';
  document.getElementById('btn-del-prod').style.display = 'none';
  document.getElementById('prod-margin-box').style.display = 'none';
  renderProductos();
}

function updateProductoMargin() {
  const cost = parseFloat(document.getElementById('prod-cost').value) || 0;
  const price = parseFloat(document.getElementById('prod-price').value) || 0;
  const box = document.getElementById('prod-margin-box');
  const val = document.getElementById('prod-margin-val');
  if (cost > 0 && price > 0) {
    const margin = ((price - cost) / price * 100).toFixed(1);
    val.textContent = margin + '% (utilidad: $' + (price - cost).toLocaleString('es-CO') + ')';
    val.style.color = margin < 10 ? 'var(--warning)' : 'var(--success)';
    box.style.display = 'block';
  } else { box.style.display = 'none'; }
}

async function saveProducto() {
  const id = document.getElementById('prod-id').value;
  const ean = document.getElementById('prod-ean').value.trim();
  const sku = document.getElementById('prod-sku').value.trim().toUpperCase();
  const name = document.getElementById('prod-name').value.trim();
  const cat = document.getElementById('prod-cat').value;
  const cost = parseFloat(document.getElementById('prod-cost').value) || 0;
  const price = parseFloat(document.getElementById('prod-price').value) || 0;
  const whMain = parseInt(document.getElementById('prod-wh-main').value) || 0;
  const whSec = parseInt(document.getElementById('prod-wh-sec').value) || 0;
  const minStock = parseInt(document.getElementById('prod-min').value) || 0;

  if (!ean || !sku || !name) { toast('EAN, SKU y Nombre son obligatorios.', 'err'); return; }
  if (price <= 0) { toast('El precio de venta debe ser mayor a cero.', 'err'); return; }

  const who = session?.Username || 'Web';

  const product = {
    Id: id || null,
    SKU: sku,
    Name: name,
    Category: cat,
    Cost: cost,
    Price: price,
    WarehouseMain: whMain,
    WarehouseSecondary: whSec,
    MinimumStock: minStock,
    UpdatedBy: who
  };

  try {
    const res = await fetch(`${API_URL}/products`, {
      method: 'POST',
      headers: getHeaders(),
      body: JSON.stringify(product)
    });

    if (res.ok) {
      toast('Producto guardado correctamente en Firestore.', 'ok');
      await refreshAllData();
      clearProductoForm();
    } else {
      toast('Error al guardar el producto.', 'err');
    }
  } catch (e) {
    console.error(e);
    toast('Error de red al conectar con la API.', 'err');
  }
}

async function toggleStatusProducto() {
  const id = document.getElementById('prod-id').value;
  if (!id) return;
  
  if (!confirm(`¿Eliminar permanentemente este producto?`)) return;

  try {
    const res = await fetch(`${API_URL}/products/${id}?updatedBy=${session.Username}`, {
      method: 'DELETE',
      headers: getHeaders()
    });

    if (res.ok) {
      toast('Producto eliminado de Firestore.', 'ok');
      await refreshAllData();
      clearProductoForm();
    } else {
      toast('Error al eliminar producto.', 'err');
    }
  } catch (e) {
    console.error(e);
  }
}

// ══════════════════════════════════════════
// VENTAS PANEL
// ══════════════════════════════════════════
function initSalesPanel() {
  buildCatalogGrid();
  buildClientSelect();
  updateCartUI();
  setTimeout(() => document.getElementById('ean-input').focus(), 100);
}

function buildClientSelect() {
  const sel = document.getElementById('sale-client');
  sel.innerHTML = '<option value="">— Consumidor Final —</option>';
  DB.customers.filter(c => c.IsActive).forEach(c => {
    const o = document.createElement('option');
    o.value = c.Id; o.textContent = c.FullName + ' | ' + c.TaxId;
    sel.appendChild(o);
  });
  updateClientInfo();
}

function updateClientInfo() {
  const id = document.getElementById('sale-client').value;
  const box = document.getElementById('client-info');
  if (!id) { box.innerHTML = '<span style="color:var(--text-dim)">Sin facturación a cliente registrado.</span>'; return; }
  const c = DB.customers.find(x => x.Id === id);
  if (!c) { box.innerHTML = ''; return; }
  box.innerHTML = `<strong>${c.FullName}</strong><br>
    NIT: ${c.TaxId} · ${c.Email}<br>
    Crédito disponible: <span style="color:${c.CreditLimit - c.OutstandingBalance < 500 ? 'var(--danger)' : 'var(--success)'}">$${(c.CreditLimit - c.OutstandingBalance).toLocaleString('es-CO')}</span>`;
}

function buildCatalogGrid() {
  const grid = document.getElementById('catalog-grid');
  grid.innerHTML = '';
  DB.products.forEach(p => {
    const total = p.WarehouseMain + p.WarehouseSecondary;
    const noStock = total === 0;
    const div = document.createElement('div');
    div.className = 'prod-chip' + (noStock ? ' no-stock' : '');
    div.onclick = () => { if (!noStock) addToCart(p.Id, 1); };
    div.innerHTML = `<div class="ean">${p.SKU}</div>
      <div class="pname">${p.Name}</div>
      <div class="pprice">$${p.Price.toLocaleString('es-CO')}</div>
      <div class="pstock ${noStock ? 'tag-low' : ''}">Stock: ${total} u.</div>`;
    grid.appendChild(div);
  });
}

function addByEan() {
  const inp = document.getElementById('ean-input');
  const val = inp.value.trim();
  const err = document.getElementById('ean-err');
  if (!val) { err.textContent = 'Ingresa un SKU o SKU.'; return; }
  const p = DB.products.find(x => x.SKU === val.toUpperCase() || x.EAN === val);
  if (!p) { err.textContent = `❌ Código '${val}' no encontrado.`; inp.select(); return; }
  err.textContent = '';
  inp.value = '';
  inp.focus();
  addToCart(p.Id, 1);
}

function simulateScan() {
  const p = DB.products.filter(x => (x.WarehouseMain + x.WarehouseSecondary) > 0);
  if (!p.length) { toast('No hay productos con stock.', 'err'); return; }
  const rnd = p[Math.floor(Math.random() * p.length)];
  addToCart(rnd.Id, 1);
}

function addToCart(productId, qty) {
  const p = DB.products.find(x => x.Id === productId);
  if (!p) return;
  const total = p.WarehouseMain + p.WarehouseSecondary;

  const existing = cart.find(l => l.ProductId === productId);
  const currentQty = existing ? existing.Qty : 0;

  if (currentQty + qty > total) {
    toast(`⚠ Stock insuficiente.`, 'err');
    return;
  }

  if (existing) {
    existing.Qty += qty;
    existing.Subtotal = existing.Qty * existing.Price * (1 - existing.Discount / 100);
  } else {
    cart.push({
      ProductId: p.Id, SKU: p.SKU, EAN: p.EAN || '', Name: p.Name,
      Price: p.Price, Qty: qty, Discount: 0,
      Subtotal: p.Price * qty
    });
  }
  toast(`✅ ${p.Name} en carrito`, 'ok');
  updateCartUI();
  buildCatalogGrid();
}

function changeQty(productId, delta) {
  const line = cart.find(l => l.ProductId === productId);
  if (!line) return;
  const p = DB.products.find(x => x.Id === productId);
  const total = p.WarehouseMain + p.WarehouseSecondary;
  const newQty = line.Qty + delta;
  if (newQty <= 0) { removeFromCart(productId); return; }
  if (newQty > total) { toast(`⚠ Stock máximo disponible: ${total} u.`, 'err'); return; }
  line.Qty = newQty;
  line.Subtotal = line.Qty * line.Price * (1 - line.Discount / 100);
  updateCartUI();
}

function changeDiscount(productId, val) {
  const line = cart.find(l => l.ProductId === productId);
  if (!line) return;
  const d = Math.min(100, Math.max(0, parseFloat(val) || 0));
  line.Discount = d;
  line.Subtotal = line.Qty * line.Price * (1 - d / 100);
  updateCartUI();
}

function removeFromCart(productId) {
  cart = cart.filter(l => l.ProductId !== productId);
  updateCartUI();
  buildCatalogGrid();
}

function clearCart(full = false) {
  if (cart.length > 0 && full && !confirm('¿Vaciar carrito?')) return;
  cart = [];
  updateCartUI();
  buildCatalogGrid();
}

function updateCartUI() {
  const empty = document.getElementById('cart-empty');
  const tbl = document.getElementById('cart-tbl');
  const body = document.getElementById('cart-body');
  const count = document.getElementById('cart-count');
  const btn = document.getElementById('btn-finalize');

  count.textContent = cart.length;
  if (!cart.length) {
    empty.style.display = 'block'; tbl.style.display = 'none';
    btn.disabled = true;
    setTotals(0, 0, 0);
    return;
  }
  empty.style.display = 'none'; tbl.style.display = 'table';
  btn.disabled = false;

  body.innerHTML = '';
  let rawSub = 0, discAmt = 0;
  cart.forEach(l => {
    rawSub += l.Price * l.Qty;
    discAmt += l.Price * l.Qty * (l.Discount / 100);
    const tr = document.createElement('tr');
    tr.innerHTML = `<td><strong>${l.Name}</strong></td>
      <td>$${l.Price.toLocaleString('es-CO')}</td>
      <td>
        <div class="qty-ctrl">
          <button class="qty-btn" onclick="changeQty('${l.ProductId}',-1)">−</button>
          <span class="qty-val">${l.Qty}</span>
          <button class="qty-btn" onclick="changeQty('${l.ProductId}',1)">+</button>
        </div>
      </td>
      <td><input type="number" value="${l.Discount}" min="0" max="100" onchange="changeDiscount('${l.ProductId}',this.value)">%</td>
      <td style="font-weight:bold;color:var(--accent)">$${l.Subtotal.toLocaleString('es-CO')}</td>
      <td><button class="del-btn" onclick="removeFromCart('${l.ProductId}')">🗑</button></td>`;
    body.appendChild(tr);
  });

  const netSub = rawSub - discAmt;
  const iva = netSub * 0.19;
  const total = netSub + iva;
  setTotals(rawSub, discAmt, iva, total);
  calcChange();
}

function setTotals(rawSub, disc, iva, total) {
  const fmt = n => ('$' + (n || 0).toLocaleString('es-CO'));
  document.getElementById('s-sub').textContent = fmt(rawSub);
  document.getElementById('s-disc').textContent = '-' + fmt(disc);
  document.getElementById('s-iva').textContent = fmt(iva);
  document.getElementById('s-total').textContent = fmt(total || 0);
}

function calcChange() {
  if (selectedPayMethod !== 'Efectivo') { document.getElementById('pay-change').textContent = 'N/A'; return; }
  const totalRaw = document.getElementById('s-total').textContent.replace(/[^0-9]/g, '');
  const total = parseFloat(totalRaw) || 0;
  const received = parseFloat(document.getElementById('pay-received').value) || 0;
  const change = received - total;
  document.getElementById('pay-change').textContent = '$' + (Math.max(0, change)).toLocaleString('es-CO');
}

function selectPay(btn, method) {
  selectedPayMethod = method;
  document.querySelectorAll('.pay-btn').forEach(b => b.classList.remove('sel'));
  btn.classList.add('sel');
  const cashRow = document.getElementById('pay-cash-row');
  cashRow.style.display = method === 'Efectivo' ? 'block' : 'none';
  calcChange();
}

// ══════════════════════════════════════════
// VENTAS — FINALIZAR COMPRA
// ══════════════════════════════════════════
async function finalizeSale() {
  if (!cart.length) return;

  let rawSub = 0, discAmt = 0;
  cart.forEach(l => { rawSub += l.Price * l.Qty; discAmt += l.Price * l.Qty * (l.Discount / 100); });
  const netSub = rawSub - discAmt;
  const iva = netSub * 0.19;
  const total = netSub + iva;

  const clientId = document.getElementById('sale-client').value;
  const customer = clientId ? DB.customers.find(c => c.Id === clientId) : null;

  saleCounter++;
  const saleNum = String(saleCounter).padStart(5, '0');
  const now = new Date().toISOString();

  const sale = {
    Id: 'sale_' + Date.now(), SaleNumber: saleNum, Date: now,
    CustomerId: clientId || null, CustomerName: customer ? customer.FullName : 'Consumidor Final',
    Lines: cart.map(l => ({ ...l })),
    RawSubtotal: rawSub, Discount: discAmt, NetSubtotal: netSub,
    IVA: iva, Total: total,
    PayMethod: selectedPayMethod,
    SoldBy: session.Username, SoldByName: session.FullName
  };

  // Descontar inventario localmente en memoria y enviarlo al backend
  for (const line of cart) {
    const p = DB.products.find(x => x.Id === line.ProductId);
    if (p) {
      let remaining = line.Qty;
      const fromMain = Math.min(remaining, p.WarehouseMain);
      p.WarehouseMain -= fromMain;
      remaining -= fromMain;
      if (remaining > 0) {
        p.WarehouseSecondary -= remaining;
      }
      // Actualizar producto en API
      await fetch(`${API_URL}/products`, {
        method: 'POST',
        headers: getHeaders(),
        body: JSON.stringify(p)
      });
    }
  }

  DB.sales.push(sale);
  await audit(session.Id, session.Username, 'Sale_Registered', `Venta #${saleNum} registrada. Total: $${total.toLocaleString('es-CO')}`);
  
  showTicket(sale);
  toast(`✅ Venta #${saleNum} registrada con éxito.`, 'ok');

  clearCart();
}

function showTicket(sale) {
  const fmt = n => '$' + (n || 0).toLocaleString('es-CO');
  document.getElementById('tk-num').textContent = sale.SaleNumber;
  document.getElementById('tk-date').textContent = new Date(sale.Date).toLocaleString('es-CO');
  document.getElementById('tk-cashier').textContent = sale.SoldByName;
  document.getElementById('tk-client').textContent = sale.CustomerName;
  document.getElementById('tk-pay').textContent = sale.PayMethod;

  const items = document.getElementById('tk-items');
  items.innerHTML = sale.Lines.map(l => `
    <div class="ticket-item">
      <span class="item-name">${l.Name}</span>
      <span>${fmt(l.Subtotal)}</span>
      <span class="item-detail">${l.Qty} u. × ${fmt(l.Price)}</span>
    </div>`).join('');

  document.getElementById('tk-sub').textContent = fmt(sale.RawSubtotal);
  document.getElementById('tk-disc').textContent = '-' + fmt(sale.Discount);
  document.getElementById('tk-iva').textContent = fmt(sale.IVA);
  document.getElementById('tk-total').textContent = fmt(sale.Total);

  document.getElementById('ticket-overlay').style.display = 'flex';
}

// ══════════════════════════════════════════
// HISTORIAL DE VENTAS
// ══════════════════════════════════════════
function renderHistory() {
  const tb = document.getElementById('t-history');
  tb.innerHTML = '';
  if (!DB.sales.length) {
    tb.innerHTML = '<tr><td colspan="10" style="text-align:center;color:var(--text-dim);padding:24px">Sin ventas registradas en esta sesión.</td></tr>';
    return;
  }
  const fmt = n => '$' + (n || 0).toLocaleString('es-CO');
  DB.sales.forEach(s => {
    const tr = document.createElement('tr');
    tr.innerHTML = `<td><strong>#${s.SaleNumber}</strong></td>
      <td>${new Date(s.Date).toLocaleString('es-CO')}</td>
      <td>${s.CustomerName}</td>
      <td style="text-align:center">${s.Lines.length}</td>
      <td>${fmt(s.RawSubtotal)}</td>
      <td>${fmt(s.IVA)}</td>
      <td style="font-weight:bold;color:var(--success)">${fmt(s.Total)}</td>
      <td><span class="tag tag-pipeline">${s.PayMethod}</span></td>
      <td>${s.SoldBy}</td>
      <td><button onclick="showTicket(${JSON.stringify(s).replace(/"/g, '&quot;')})" class="btn btn-sm btn-secondary">📄 Re-Imprimir</button></td>`;
    tb.appendChild(tr);
  });
}

// ══════════════════════════════════════════
// CLIENTES (CRM)
// ══════════════════════════════════════════
function renderCRM() {
  const tb = document.getElementById('t-crm');
  tb.innerHTML = '';
  DB.customers.forEach(c => {
    const tr = document.createElement('tr');
    tr.onclick = () => selectCustomer(c);
    tr.innerHTML = `<td><strong>${c.FullName}</strong><br><span style="font-size:10px;color:var(--text-dim)">${c.Email}</span></td>
      <td>${c.TaxId}</td>
      <td><span class="tag tag-pipeline">${c.PipelineStage}</span></td>
      <td>$${c.OutstandingBalance.toLocaleString('es-CO')}</td>
      <td><span class="tag ${c.IsActive ? 'tag-ok' : 'tag-inactive'}">${c.IsActive ? 'ACTIVO' : 'INACTIVO'}</span></td>`;
    tb.appendChild(tr);
  });
}

function selectCustomer(c) {
  document.getElementById('c-id').value = c.Id;
  document.getElementById('c-name').value = c.FullName;
  document.getElementById('c-taxid').value = c.TaxId;
  document.getElementById('c-email').value = c.Email;
  document.getElementById('c-phone').value = c.Phone;
  document.getElementById('c-stage').value = c.PipelineStage;
  document.getElementById('c-credit').value = c.CreditLimit;
  document.getElementById('c-balance').value = c.OutstandingBalance;
  document.getElementById('c-active').checked = c.IsActive;
  document.getElementById('crm-form-title').textContent = 'Editar Cliente';
  document.getElementById('btn-deactivate').style.display = 'inline-flex';
}

function clearCRMForm() {
  ['c-id', 'c-name', 'c-taxid', 'c-email', 'c-phone'].forEach(id => document.getElementById(id).value = '');
  document.getElementById('c-stage').value = 'Prospecto';
  document.getElementById('c-credit').value = '';
  document.getElementById('c-balance').value = '0';
  document.getElementById('c-active').checked = true;
  document.getElementById('crm-form-title').textContent = 'Nuevo Cliente';
  document.getElementById('btn-deactivate').style.display = 'none';
}

async function saveCustomer() {
  const id = document.getElementById('c-id').value;
  const name = document.getElementById('c-name').value.trim();
  const taxid = document.getElementById('c-taxid').value.trim();
  const email = document.getElementById('c-email').value.trim();
  const phone = document.getElementById('c-phone').value.trim();
  const stage = document.getElementById('c-stage').value;
  const credit = parseFloat(document.getElementById('c-credit').value) || 0;
  const balance = parseFloat(document.getElementById('c-balance').value) || 0;
  const isActive = document.getElementById('c-active').checked;

  if (!name || !taxid || !email) { toast('Campos obligatorios vacíos.', 'err'); return; }

  const customer = {
    Id: id || null, FullName: name, TaxId: taxid, Email: email, Phone: phone,
    PipelineStage: stage, CreditLimit: credit, OutstandingBalance: balance, IsActive: isActive,
    UpdatedBy: session.Username
  };

  try {
    const res = await fetch(`${API_URL}/customers`, {
      method: 'POST',
      headers: getHeaders(),
      body: JSON.stringify(customer)
    });
    if (res.ok) {
      toast('Cliente guardado en Firestore.', 'ok');
      await refreshAllData();
      clearCRMForm();
    }
  } catch (e) {
    console.error(e);
  }
}

async function toggleCustomerActive() {
  const id = document.getElementById('c-id').value;
  if (!id) return;
  const c = DB.customers.find(x => x.Id === id);
  if (!c) return;
  c.IsActive = !c.IsActive;
  
  try {
    await fetch(`${API_URL}/customers`, {
      method: 'POST',
      headers: getHeaders(),
      body: JSON.stringify(c)
    });
    toast('Estado del cliente actualizado.', 'ok');
    await refreshAllData();
    clearCRMForm();
  } catch (e) {
    console.error(e);
  }
}

// ══════════════════════════════════════════
// PROVEEDORES
// ══════════════════════════════════════════
function renderProveedores() {
  const tb = document.getElementById('t-prov');
  tb.innerHTML = '';
  DB.suppliers.forEach(p => {
    const tr = document.createElement('tr');
    tr.onclick = () => selectProveedor(p);
    tr.innerHTML = `<td><strong>${p.FullName}</strong><br><span style="font-size:10px;color:var(--text-dim)">${p.Email}</span></td>
      <td>${p.TaxId}</td>
      <td>${p.ContactPerson || '—'}</td>
      <td>${p.Phone || '—'}</td>
      <td><span class="tag ${p.IsActive ? 'tag-ok' : 'tag-inactive'}">${p.IsActive ? 'ACTIVO' : 'INACTIVO'}</span></td>`;
    tb.appendChild(tr);
  });
}

function selectProveedor(p) {
  document.getElementById('pv-id').value = p.Id;
  document.getElementById('pv-name').value = p.FullName;
  document.getElementById('pv-taxid').value = p.TaxId;
  document.getElementById('pv-email').value = p.Email;
  document.getElementById('pv-phone').value = p.Phone;
  document.getElementById('pv-address').value = p.Address || '';
  document.getElementById('pv-contact').value = p.ContactPerson || '';
  document.getElementById('pv-active').checked = p.IsActive;
  document.getElementById('prov-form-title').textContent = 'Editar Proveedor';
  document.getElementById('btn-deactivate-prov').style.display = 'inline-flex';
}

function clearProveedorForm() {
  ['pv-id', 'pv-name', 'pv-taxid', 'pv-email', 'pv-phone', 'pv-address', 'pv-contact'].forEach(id => document.getElementById(id).value = '');
  document.getElementById('pv-active').checked = true;
  document.getElementById('prov-form-title').textContent = 'Nuevo Proveedor';
  document.getElementById('btn-deactivate-prov').style.display = 'none';
}

async function saveProveedor() {
  const id = document.getElementById('pv-id').value;
  const name = document.getElementById('pv-name').value.trim();
  const taxid = document.getElementById('pv-taxid').value.trim();
  const email = document.getElementById('pv-email').value.trim();
  const phone = document.getElementById('pv-phone').value.trim();
  const address = document.getElementById('pv-address').value.trim();
  const contact = document.getElementById('pv-contact').value.trim();
  const isActive = document.getElementById('pv-active').checked;

  if (!name || !taxid || !email) { toast('Campos obligatorios vacíos.', 'err'); return; }

  const supplier = {
    Id: id || null, FullName: name, TaxId: taxid, Email: email, Phone: phone,
    Address: address, ContactPerson: contact, IsActive: isActive,
    UpdatedBy: session.Username
  };

  try {
    const res = await fetch(`${API_URL}/suppliers`, {
      method: 'POST',
      headers: getHeaders(),
      body: JSON.stringify(supplier)
    });
    if (res.ok) {
      toast('Proveedor guardado en Firestore.', 'ok');
      await refreshAllData();
      clearProveedorForm();
    }
  } catch (e) {
    console.error(e);
  }
}

async function toggleProveedorActive() {
  const id = document.getElementById('pv-id').value;
  if (!id) return;
  const p = DB.suppliers.find(x => x.Id === id);
  if (!p) return;
  p.IsActive = !p.IsActive;

  try {
    await fetch(`${API_URL}/suppliers`, {
      method: 'POST',
      headers: getHeaders(),
      body: JSON.stringify(p)
    });
    toast('Estado del proveedor actualizado.', 'ok');
    await refreshAllData();
    clearProveedorForm();
  } catch (e) {
    console.error(e);
  }
}

// ══════════════════════════════════════════
// AUDITORÍA VISTA
// ══════════════════════════════════════════
function renderAudit() {
  const tb = document.getElementById('t-audit');
  if (!tb) return;
  tb.innerHTML = '';
  DB.audit.forEach(l => {
    const tr = document.createElement('tr');
    const t = l.EventTime ? l.EventTime.slice(0, 19).replace('T', ' ') : '';
    const color = l.EventType.includes('Failed') || l.EventType.includes('Denied') ? 'var(--danger)' : 'var(--success)';
    tr.innerHTML = `<td>${t}</td>
      <td><span style="color:${color};font-weight:bold">${l.EventType}</span></td>
      <td>${l.Username}</td>
      <td>${l.UserId}</td>
      <td>${l.MachineName}</td>
      <td>${l.Description}</td>`;
    tb.appendChild(tr);
  });
}

// ══════════════════════════════════════════
// USUARIOS PANEL VISTA
// ══════════════════════════════════════════
function renderUsers() {
  const tb = document.getElementById('t-usr');
  tb.innerHTML = '<tr><td colspan="5" style="text-align:center;color:var(--text-dim)">Gestionado remotamente por Firestore.</td></tr>';
}

// ══════════════════════════════════════════
// ENTRADAS / COMPRAS
// ══════════════════════════════════════════
function initComprasPanel() {
  buildPurchaseCatalogGrid();
  buildPurchaseSupplierSelect();
  updatePurchaseCartUI();
}

function buildPurchaseSupplierSelect() {
  const sel = document.getElementById('purchase-supplier');
  if (!sel) return;
  sel.innerHTML = '<option value="">— Seleccionar Proveedor —</option>';
  DB.suppliers.filter(p => p.IsActive).forEach(p => {
    const o = document.createElement('option');
    o.value = p.Id; o.textContent = p.FullName;
    sel.appendChild(o);
  });
}

function buildPurchaseCatalogGrid() {
  const grid = document.getElementById('purchase-catalog-grid');
  if (!grid) return;
  grid.innerHTML = '';
  DB.products.forEach(p => {
    const div = document.createElement('div');
    div.className = 'prod-chip';
    div.onclick = () => addToPurchaseCart(p.Id, 1);
    div.innerHTML = `<div class="ean">${p.SKU}</div>
      <div class="pname">${p.Name}</div>
      <div class="pprice">Costo: $${p.Cost.toLocaleString('es-CO')}</div>`;
    grid.appendChild(div);
  });
}

function addToPurchaseCart(productId, qty) {
  const p = DB.products.find(x => x.Id === productId);
  if (!p) return;
  const existing = purchaseCart.find(l => l.ProductId === productId);
  if (existing) {
    existing.Qty += qty;
    existing.Subtotal = existing.Qty * existing.Cost;
  } else {
    purchaseCart.push({
      ProductId: p.Id, SKU: p.SKU, Name: p.Name,
      Cost: p.Cost, Qty: qty, Subtotal: p.Cost * qty
    });
  }
  updatePurchaseCartUI();
}

function updatePurchaseCartUI() {
  const empty = document.getElementById('purchase-cart-empty');
  const tbl = document.getElementById('purchase-cart-tbl');
  const body = document.getElementById('purchase-cart-body');
  const btn = document.getElementById('btn-finalize-purchase');

  if (!purchaseCart.length) {
    empty.style.display = 'block'; tbl.style.display = 'none';
    btn.disabled = true;
    return;
  }
  empty.style.display = 'none'; tbl.style.display = 'table';
  btn.disabled = false;

  body.innerHTML = '';
  let total = 0;
  purchaseCart.forEach(l => {
    total += l.Subtotal;
    const tr = document.createElement('tr');
    tr.innerHTML = `<td><strong>${l.Name}</strong></td>
      <td>${l.SKU}</td>
      <td>$${l.Cost.toLocaleString('es-CO')}</td>
      <td>${l.Qty}</td>
      <td>$${l.Subtotal.toLocaleString('es-CO')}</td>
      <td><button onclick="removeFromPurchaseCart('${l.ProductId}')">🗑</button></td>`;
    body.appendChild(tr);
  });
  document.getElementById('p-total').textContent = '$' + total.toLocaleString('es-CO');
}

function removeFromPurchaseCart(productId) {
  purchaseCart = purchaseCart.filter(l => l.ProductId !== productId);
  updatePurchaseCartUI();
}

async function finalizePurchase() {
  const supplierId = document.getElementById('purchase-supplier').value;
  if (!supplierId) { toast('Selecciona un proveedor.', 'err'); return; }

  for (const line of purchaseCart) {
    const p = DB.products.find(x => x.Id === line.ProductId);
    if (p) {
      if (document.getElementById('purchase-warehouse').value === 'Main') {
        p.WarehouseMain += line.Qty;
      } else {
        p.WarehouseSecondary += line.Qty;
      }
      p.Cost = line.Cost;
      await fetch(`${API_URL}/products`, {
        method: 'POST',
        headers: getHeaders(),
        body: JSON.stringify(p)
      });
    }
  }

  toast('✅ Inventario actualizado en Firestore.', 'ok');
  await refreshAllData();
  purchaseCart = [];
  updatePurchaseCartUI();
}

window.onload = initDB;
