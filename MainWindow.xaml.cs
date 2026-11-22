using System;
using System.Data;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using CefSharp;
using CefSharp.Wpf;
using PatientAppointmentManager.Data;
using PatientAppointmentManager.Models;

namespace PatientAppointmentManager
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Database.Initialize();
            UpdateSidebarSelection("add");

            AppointmentDatePicker.SelectedDate = DateTime.Today.AddDays(1);
            FromDatePicker.SelectedDate = DateTime.Today;
            ToDatePicker.SelectedDate = DateTime.Today;

            ConfigureBrowserTab(FirstBrowserTab, AddHospitalBrowser, "Trang 1");
            RegisterBrowser(AddHospitalBrowser);
            LoadBrowserHistory(AddUrlBox);
        }

        private void ShowView(string name)
        {
            AddView.Visibility = name == "add" ? Visibility.Visible : Visibility.Collapsed;
            ListView.Visibility = name == "list" ? Visibility.Visible : Visibility.Collapsed;

            UpdateSidebarSelection(name);
            if (name == "list")
                LoadAppointments();
        }

        private void UpdateSidebarSelection(string selected)
        {
            SetNavButtonState(AddNavButton, selected == "add");
            SetNavButtonState(ListNavButton, selected == "list");
        }

        private static void SetNavButtonState(Button button, bool selected)
        {
            button.Background = selected
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(190, 235, 229))
                : System.Windows.Media.Brushes.White;
            button.BorderBrush = selected
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(63, 174, 160))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(214, 232, 232));
            button.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
            button.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 125, 125));
        }

        private ChromiumWebBrowser ActiveBrowser
        {
            get
            {
                var item = BrowserTabsControl.SelectedItem as TabItem;
                return item == null ? AddHospitalBrowser : item.Content as ChromiumWebBrowser;
            }
        }

        private void RegisterBrowser(ChromiumWebBrowser browser)
        {
            browser.LifeSpanHandler = new TabLifeSpanHandler(targetUrl =>
            {
                Dispatcher.BeginInvoke(new Action(() => CreateBrowserTab(targetUrl)));
            });
            browser.FrameLoadEnd += (sender, args) =>
            {
                if (!args.Frame.IsMain) return;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (ReferenceEquals(browser, ActiveBrowser))
                        AddUrlBox.Text = args.Url;
                    SaveVisitedAddress(args.Url, AddUrlBox);
                }));
            };
        }

        private void ConfigureBrowserTab(TabItem tab, ChromiumWebBrowser browser, string title)
        {
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) });
            var close = new Button { Content = "×", Width = 20, Height = 20, Padding = new Thickness(0), FontSize = 13, ToolTip = "Đóng tab" };
            close.Click += (s, e) => CloseBrowserTab(tab, browser);
            header.Children.Add(close);
            tab.Header = header;
        }

        private void CloseBrowserTab(TabItem tab, ChromiumWebBrowser browser)
        {
            if (BrowserTabsControl.Items.Count <= 1) return;
            BrowserTabsControl.Items.Remove(tab);
            if (browser != null) browser.Dispose();
        }

        private void CreateBrowserTab(string address)
        {
            var browser = new ChromiumWebBrowser { Address = string.IsNullOrWhiteSpace(address) ? "about:blank" : address };
            RegisterBrowser(browser);
            var tab = new TabItem { Content = browser };
            ConfigureBrowserTab(tab, browser, "Trang " + (BrowserTabsControl.Items.Count + 1));
            BrowserTabsControl.Items.Add(tab);
            BrowserTabsControl.SelectedItem = tab;
            AddUrlBox.Text = browser.Address ?? string.Empty;
        }

        private void LoadBrowserHistory(ComboBox box)
        {
            try
            {
                box.Items.Clear();
                foreach (string address in Database.GetBrowserHistory(30))
                    box.Items.Add(address);
            }
            catch { /* Lịch sử không được làm gián đoạn khởi động ứng dụng. */ }
        }

        private void SaveVisitedAddress(string address, ComboBox box)
        {
            if (string.IsNullOrWhiteSpace(address) || address == "about:blank") return;
            try
            {
                Database.SaveBrowserHistory(address);
                string current = box.Text;
                LoadBrowserHistory(box);
                box.Text = current;
            }
            catch { }
        }

        private void BrowserTabsControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || e.Source != BrowserTabsControl) return;
            var browser = ActiveBrowser;
            if (browser != null)
                AddUrlBox.Text = browser.Address ?? string.Empty;
        }

        private void NewBrowserTab_Click(object sender, RoutedEventArgs e)
        {
            CreateBrowserTab("about:blank");
            AddUrlBox.Focus();
        }

        private void AddTab_Click(object sender, RoutedEventArgs e) => ShowView("add");
        private void ListTab_Click(object sender, RoutedEventArgs e) => ShowView("list");

        private bool TryGetWebAddress(string input, out string address)
        {
            address = (input ?? "").Trim();

            if (address.Length == 0)
                return false;

            if (!address.Contains("://"))
                address = "http://" + address;

            Uri uri;

            if (!Uri.TryCreate(address, UriKind.Absolute, out uri))
                return false;

            if (uri.Scheme != Uri.UriSchemeHttp &&
                uri.Scheme != Uri.UriSchemeHttps)
                return false;

            address = uri.AbsoluteUri;
            return true;
        }

        private void AddUrlBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (AddUrlBox.Items.Count > 0) AddUrlBox.IsDropDownOpen = true;
        }

        // Chỉ điều hướng khi người dùng click chuột vào một mục lịch sử.
        // Di chuyển bằng phím mũi tên không tự ý tải trang.
        private void HistoryItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var item = sender as ComboBoxItem;
            if (item == null) return;

            var box = ItemsControl.ItemsControlFromItemContainer(item) as ComboBox;
            if (box == null) return;

            string address = Convert.ToString(item.Content);
            if (string.IsNullOrWhiteSpace(address)) return;

            box.SelectedItem = address;
            box.Text = address;
            box.IsDropDownOpen = false;
            box.Dispatcher.BeginInvoke(new Action(() => box.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next))), System.Windows.Threading.DispatcherPriority.Input);

            if (box == AddUrlBox)
            {
                string normalized;
                if (TryGetWebAddress(address, out normalized) && ActiveBrowser != null)
                    ActiveBrowser.Address = normalized;
            }

            e.Handled = true;
        }

        private void AddUrlBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                AddOpenUrl_Click(sender, new RoutedEventArgs());
            }
        }

        private void OpenUrl_Click(object sender, RoutedEventArgs e)
        {
            string address;

            if (!TryGetWebAddress(AddUrlBox.Text, out address))
            {
                ShowAppMessage("Địa chỉ website không hợp lệ.");
                return;
            }

            ActiveBrowser.Address = address;
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveBrowser != null && ActiveBrowser.CanGoBack) ActiveBrowser.Back();
        }

        private void Forward_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveBrowser != null && ActiveBrowser.CanGoForward) ActiveBrowser.Forward();
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveBrowser != null) ActiveBrowser.Reload();
        }

        private ChromiumWebBrowser CredentialBrowser
        {
            get { return ActiveBrowser; }
        }

        private string CurrentBrowserHost()
        {
            Uri uri;
            string address = CredentialBrowser == null ? null : CredentialBrowser.Address;
            return Uri.TryCreate(address, UriKind.Absolute, out uri) ? uri.Host.ToLowerInvariant() : null;
        }

        private static byte[] CredentialEntropy(string host)
        {
            return Encoding.UTF8.GetBytes("SoHenCT|" + (host ?? string.Empty));
        }

        private void SaveLogin_Click(object sender, RoutedEventArgs e)
        {
            string host = CurrentBrowserHost();
            if (string.IsNullOrWhiteSpace(host))
            {
                ShowAppMessage("Hãy mở trang đăng nhập trước khi lưu tài khoản.");
                return;
            }

            var dialog = new Window
            {
                Title = "Lưu đăng nhập cho " + host,
                Width = 420, Height = 270, ResizeMode = ResizeMode.NoResize,
                Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = System.Windows.Media.Brushes.White
            };
            var panel = new StackPanel { Margin = new Thickness(18) };
            panel.Children.Add(new TextBlock { Text = "Tên đăng nhập", Margin = new Thickness(0, 0, 0, 5) });
            var userBox = new TextBox { Height = 34, Margin = new Thickness(0, 0, 0, 12) };
            panel.Children.Add(userBox);
            panel.Children.Add(new TextBlock { Text = "Mật khẩu", Margin = new Thickness(0, 0, 0, 5) });
            var passBox = new PasswordBox { Height = 34, Margin = new Thickness(0, 0, 0, 14) };
            panel.Children.Add(passBox);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var save = new Button { Content = "Lưu mã hóa", Width = 110, Height = 34, Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "Hủy", Width = 80, Height = 34 };
            save.Click += (s, args) =>
            {
                if (string.IsNullOrWhiteSpace(userBox.Text) || string.IsNullOrEmpty(passBox.Password))
                {
                    ShowAppMessage(dialog, "Nhập cả tên đăng nhập và mật khẩu.");
                    return;
                }
                try
                {
                    byte[] raw = Encoding.UTF8.GetBytes(userBox.Text + "\n" + passBox.Password);
                    byte[] protectedBytes = ProtectedData.Protect(raw, CredentialEntropy(host), DataProtectionScope.CurrentUser);
                    Database.SaveProtectedLogin(host, Convert.ToBase64String(protectedBytes));
                    Array.Clear(raw, 0, raw.Length);
                    passBox.Clear();
                    dialog.DialogResult = true;
                    dialog.Close();
                }
                catch (Exception ex) { ShowAppMessage(dialog, "Không thể lưu thông tin đăng nhập: " + ex.Message); }
            };
            cancel.Click += (s, args) => dialog.Close();
            buttons.Children.Add(save); buttons.Children.Add(cancel); panel.Children.Add(buttons);
            dialog.Content = panel;
            dialog.ShowDialog();
            if (dialog.DialogResult == true)
                ShowAppMessage("Đã lưu thông tin đăng nhập đã mã hóa cho " + host + ". Chỉ tài khoản Windows hiện tại giải mã được.");
        }

        private async void FillLogin_Click(object sender, RoutedEventArgs e)
        {
            string host = CurrentBrowserHost();
            if (string.IsNullOrWhiteSpace(host)) { ShowAppMessage("Hãy mở website cần đăng nhập."); return; }
            try
            {
                string stored = Database.GetProtectedLogin(host);
                if (string.IsNullOrWhiteSpace(stored))
                {
                    ShowAppMessage("Chưa có tài khoản được lưu cho website này. Hãy chọn Lưu đăng nhập trước.", "Thông tin đăng nhập");
                    return;
                }
                byte[] protectedBytes = Convert.FromBase64String(stored);
                byte[] raw = ProtectedData.Unprotect(protectedBytes, CredentialEntropy(host), DataProtectionScope.CurrentUser);
                string[] parts = Encoding.UTF8.GetString(raw).Split(new[] { '\n' }, 2);
                Array.Clear(raw, 0, raw.Length);
                if (parts.Length != 2) throw new InvalidOperationException("Dữ liệu đăng nhập không hợp lệ.");
                string user = EscapeJavaScript(parts[0]);
                string pass = EscapeJavaScript(parts[1]);
                string script = "(function(){var p=document.querySelector('input[type=password]');if(!p)return 'Không tìm thấy ô mật khẩu';var u=document.querySelector('input[type=email],input[autocomplete=username],input[type=text]');function set(el,v){if(!el)return;var d=Object.getOwnPropertyDescriptor(Object.getPrototypeOf(el),'value');if(d&&d.set)d.set.call(el,v);else el.value=v;el.dispatchEvent(new Event('input',{bubbles:true}));el.dispatchEvent(new Event('change',{bubbles:true}));}set(u,'" + user + "');set(p,'" + pass + "');return 'Đã điền thông tin, chưa tự gửi biểu mẫu';})()";
                var frame = CredentialBrowser.GetMainFrame();
                var result = await frame.EvaluateScriptAsync(script);
                ShowAppMessage(Convert.ToString(result.Result ?? "Không thể tự điền"), "Tự điền đăng nhập");
            }
            catch (Exception ex) { ShowAppMessage("Không thể giải mã hoặc điền thông tin: " + ex.Message); }
        }

        private static string EscapeJavaScript(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private void BrowserFit_Click(object sender, RoutedEventArgs e) { if (ActiveBrowser != null) ActiveBrowser.ZoomLevel = 0; }
        private void BrowserZoomIn_Click(object sender, RoutedEventArgs e) { if (ActiveBrowser != null) ActiveBrowser.ZoomLevel = Math.Min(5, ActiveBrowser.ZoomLevel + 0.5); }
        private void BrowserZoomOut_Click(object sender, RoutedEventArgs e) { if (ActiveBrowser != null) ActiveBrowser.ZoomLevel = Math.Max(-5, ActiveBrowser.ZoomLevel - 0.5); }
        private void AddBrowserFit_Click(object sender, RoutedEventArgs e) { BrowserFit_Click(sender, e); }
        private void AddBrowserZoomIn_Click(object sender, RoutedEventArgs e) { BrowserZoomIn_Click(sender, e); }
        private void AddBrowserZoomOut_Click(object sender, RoutedEventArgs e) { BrowserZoomOut_Click(sender, e); }

        private void AddOpenUrl_Click(object sender, RoutedEventArgs e)
        {
            string address;

            if (!TryGetWebAddress(AddUrlBox.Text, out address))
            {
                ShowAppMessage("Địa chỉ website không hợp lệ.");
                return;
            }

            ActiveBrowser.Address = address;
        }

        private void AddBack_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveBrowser != null && ActiveBrowser.CanGoBack) ActiveBrowser.Back();
        }

        private void AddForward_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveBrowser != null && ActiveBrowser.CanGoForward) ActiveBrowser.Forward();
        }

        private void AddReload_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveBrowser != null) ActiveBrowser.Reload();
        }

        private sealed class HospitalScanRow
        {
            public string ReceiptCode { get; set; }
            public string ExamCode { get { return ReceiptCode; } }
            public string PatientCode { get; set; }
            public string PatientName { get; set; }
            public string Age { get; set; }
            public string InjectionStatus { get; set; }
            public string Department { get; set; }
            public string OrderedAt { get; set; }
            public string ServiceName { get; set; }
            public string Room { get; set; }
            public string Status { get; set; }
            public long ExistingAppointmentId { get; set; }
            public string ExistingAppointmentDate { get; set; }
            public string ExistingShift { get; set; }
        }

        private const string HospitalScanScript = @"
(function () {
  function clean(v) { return (v || '').replace(/\s+/g, ' ').trim(); }
  function txt(el) { return clean(el ? (el.innerText || el.textContent || '') : ''); }
  function visible(el) {
    if (!el) return false;
    var st = window.getComputedStyle(el);
    return st.display !== 'none' && st.visibility !== 'hidden' && el.getClientRects().length > 0;
  }
  function norm(v) {
    var x = clean(v).toLowerCase();
    try { x = x.normalize('NFD').replace(/[\u0300-\u036f]/g, ''); } catch(e) {}
    return x.replace(/đ/g, 'd');
  }
  function serviceCandidate(value, shown) {
    var v = clean(value);
    if (!v || v.length <= shown.length) return '';
    var n = norm(v);
    // Ưu tiên chuỗi mô tả dịch vụ có cùng phần đầu với nội dung đang hiển thị.
    var prefix = norm(shown).replace(/[.…]+$/g, '').trim();
    if (prefix && n.indexOf(prefix.substring(0, Math.min(prefix.length, 18))) < 0 &&
        n.indexOf('chup') < 0 && n.indexOf('dich vu') < 0) return '';
    if (v.length > 1200) return '';
    return v;
  }
  function deepServiceStrings(root, shown) {
    var found = [], seen = [];
    function walk(obj, depth) {
      if (obj == null || depth > 5) return;
      if (typeof obj === 'string') {
        var candidate = serviceCandidate(obj, shown);
        if (candidate) found.push(candidate);
        return;
      }
      if (typeof obj !== 'object' || seen.indexOf(obj) >= 0) return;
      seen.push(obj);
      var keys;
      try { keys = Object.keys(obj).slice(0, 80); } catch(e) { return; }
      for (var i=0;i<keys.length;i++) {
        var key = keys[i];
        if (/^(_owner|_store|return|child|sibling|stateNode|alternate|dependencies)$/i.test(key)) continue;
        try { walk(obj[key], depth + 1); } catch(e) {}
      }
    }
    walk(root, 0);
    found.sort(function(a,b){ return b.length-a.length; });
    return found.length ? found[0] : '';
  }
  function getFullService(cell, shown) {
    if (!cell) return shown;
    var candidates = [];
    // Thuộc tính title/aria-label/data-* có thể giữ nguyên nội dung trước khi CSS rút gọn.
    var nodes = [cell].concat(Array.prototype.slice.call(cell.querySelectorAll('*')));
    nodes.forEach(function(el) {
      ['title','aria-label','data-original-title','data-title','data-content','data-tooltip-content'].forEach(function(a) {
        try { var v = serviceCandidate(el.getAttribute(a), shown); if (v) candidates.push(v); } catch(e) {}
      });
      // React lưu dữ liệu gốc trong props/fiber dù textContent đã bị rút gọn.
      try {
        Object.keys(el).forEach(function(k) {
          if (k.indexOf('__reactProps$') === 0) {
            var v = deepServiceStrings(el[k], shown); if (v) candidates.push(v);
          }
          if (k.indexOf('__reactFiber$') === 0) {
            var fiber = el[k], hops = 0;
            while (fiber && hops++ < 8) {
              var v1 = deepServiceStrings(fiber.memoizedProps, shown);
              var v2 = deepServiceStrings(fiber.pendingProps, shown);
              if (v1) candidates.push(v1); if (v2) candidates.push(v2);
              fiber = fiber.return;
            }
          }
        });
      } catch(e) {}
    });
    // Đọc Popover theo aria-describedby nếu phần tử đã tồn tại, không giả lập rê chuột.
    var serviceSpan = cell.querySelector('span[aria-describedby]');
    if (serviceSpan) {
      var describedId = serviceSpan.getAttribute('aria-describedby');
      var describedNode = describedId ? document.getElementById(describedId) : null;
      if (describedNode) {
        var popoverInner = describedNode.querySelector('.ant-popover-inner') ||
                           (describedNode.classList && describedNode.classList.contains('ant-popover-inner') ? describedNode : null);
        var fullText = txt(popoverInner || describedNode).replace(/^Tên dịch vụ\s*/i, '').trim();
        var c = serviceCandidate(fullText, shown); if (c) candidates.push(c);
      }
    }
    candidates.sort(function(a,b){ return b.length-a.length; });
    return candidates.length ? candidates[0] : shown;
  }

  // Selector đã được xác nhận bằng công cụ DOM trên website bệnh viện.
  var tables = Array.prototype.slice.call(document.querySelectorAll('table._st-table_gefxy_208'));
  if (!tables.length) tables = Array.prototype.slice.call(document.querySelectorAll('table'));
  tables = tables.filter(visible);
  if (!tables.length) return JSON.stringify({error:'Không tìm thấy bảng chỉ định. Hãy mở trang danh sách bệnh nhân.'});

  var chosen = null, bestRows = [], bestScore = -1, bestHeaders = [];
  tables.forEach(function(t) {
    var allRows = Array.prototype.slice.call(t.querySelectorAll('tr'));
    var dataRows = allRows.filter(function(r) {
      var cells = r.querySelectorAll('td');
      return cells.length >= 13 && visible(r);
    });
    var headers = [];
    var head = t.querySelector('thead tr');
    if (head) {
      var hc = head.querySelectorAll('th,td');
      for (var i=0;i<hc.length;i++) headers.push(txt(hc[i]));
    }
    // Bảng đã xác nhận có 13 cột, gồm STT ở vị trí 0.
    var score = dataRows.length * 10 + (t.className && String(t.className).indexOf('_st-table_gefxy_208') >= 0 ? 100 : 0);
    if (score > bestScore) { chosen=t; bestRows=dataRows; bestScore=score; bestHeaders=headers; }
  });
  if (!chosen) return JSON.stringify({error:'Không tìm thấy bảng chỉ định phù hợp.'});

  var rows = [];
  bestRows.forEach(function(r) {
    var c = r.querySelectorAll('td');
    function at(i) { return i < c.length ? txt(c[i]) : ''; }
    // Cột theo DOM đã xác nhận: 0 STT, 1 Số phiếu, 2 Mã lần khám,
    // 3 Mã BN, 4 Tên BN, 5 Tuổi, 6 Khoa/phòng, 7 Thời gian,
    // 8 Tên dịch vụ, 9 Phòng, 10 KTV, 11 Thứ tự, 12 Thao tác.
    var serviceCell = c.length > 8 ? c[8] : null;
    var displayedService = at(8);
    var serviceName = getFullService(serviceCell, displayedService);
    var item = {
      receiptCode: at(1),
      patientCode: at(3),
      patientName: at(4),
      age: at(5),
      department: at(6),
      orderedAt: at(7),
      serviceName: serviceName,
      room: at(9)
    };
    if (item.receiptCode && item.patientCode) rows.push(item);
  });
  return JSON.stringify({
    headers: bestHeaders,
    rowCount: rows.length,
    rows: rows,
    debug: {selector: 'table._st-table_gefxy_208', candidateRows: bestRows.length,
            acceptedRows: rows.length, cellCountExpected: 13}
  });
})()";

        private const string AutoDetectAppointmentPairsScript = @"
(function(){
 function clean(v){return (v||'').replace(/\s+/g,' ').trim();}
 function norm(v){var x=clean(v).toLowerCase();try{x=x.normalize('NFD').replace(/[\u0300-\u036f]/g,'');}catch(e){}return x.replace(/đ/g,'d');}
 function text(el){return clean(el?(el.innerText||el.textContent||''):'');}
 var result=[], seen={};
 function add(patient,receipt,name,dept,service,age){
  patient=clean(patient);receipt=clean(receipt);
  if(!patient||!receipt||patient.length<3||receipt.length<3||!/[0-9]/.test(patient)||!/[0-9]/.test(receipt))return;
  var key=patient+'|'+receipt;if(seen[key])return;seen[key]=1;
  result.push({patientCode:patient,receiptCode:receipt,patientName:clean(name),department:clean(dept),serviceName:clean(service),age:clean(age)});
 }
 // Bảng chỉ định của bệnh viện đã được xác nhận: 13 cột cố định.
 var known=Array.prototype.slice.call(document.querySelectorAll('table._st-table_gefxy_208'));
 known.forEach(function(table){
  Array.prototype.slice.call(table.querySelectorAll('tr')).forEach(function(tr){
   var c=Array.prototype.slice.call(tr.querySelectorAll('td'));if(c.length<13)return;
   function at(i){return text(c[i]);}
   add(at(3),at(1),at(4),at(6),at(8),at(5));
  });
 });
 // Các bảng kết quả tìm bệnh nhân: dò theo tiêu đề thay vì phụ thuộc thứ tự cột.
 var tables=Array.prototype.slice.call(document.querySelectorAll('table'));
 tables.forEach(function(table){
  if(known.indexOf(table)>=0)return;
  var headerRow=table.querySelector('thead tr')||table.querySelector('tr');if(!headerRow)return;
  var hs=Array.prototype.slice.call(headerRow.querySelectorAll('th,td')).map(text);
  var pi=-1,ri=-1,ni=-1,di=-1,si=-1,ai=-1;
  hs.forEach(function(h,i){var n=norm(h);
   if(pi<0&&(n==='ma bn'||n.indexOf('ma benh nhan')>=0))pi=i;
   if(ri<0&&(n.indexOf('so phieu')>=0||n.indexOf('ma chi dinh')>=0))ri=i;
   if(ni<0&&(n.indexOf('ten benh nhan')>=0||n==='ho ten'))ni=i;
   if(di<0&&(n.indexOf('khoa')>=0||n.indexOf('phong')>=0))di=i;
   if(si<0&&(n.indexOf('dich vu')>=0||n.indexOf('ten chi dinh')>=0))si=i;
   if(ai<0&&n.indexOf('tuoi')>=0)ai=i;
  });
  if(pi<0||ri<0)return;
  var trs=Array.prototype.slice.call(table.querySelectorAll('tbody tr'));
  if(!trs.length)trs=Array.prototype.slice.call(table.querySelectorAll('tr')).slice(1);
  trs.forEach(function(tr){var c=Array.prototype.slice.call(tr.querySelectorAll('td'));if(!c.length)return;
   function at(i){return i>=0&&i<c.length?text(c[i]):'';}
   add(at(pi),at(ri),at(ni),at(di),at(si),at(ai));
  });
 });
 return JSON.stringify(result);
})()";

        private void ShowDuplicateNotice(HospitalScanRow row, Appointment saved)
        {
            // Thông báo trùng lịch gọn, dùng cùng bảng màu xanh đậm/xanh ngọc của ứng dụng.
            var dialog = new Window
            {
                Title = "Phát hiện bệnh nhân đã có lịch",
                Width = 470,
                SizeToContent = SizeToContent.Height,
                MinHeight = 190,
                MaxHeight = 300,
                ResizeMode = ResizeMode.NoResize,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = System.Windows.Media.Brushes.Transparent,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                ShowInTaskbar = false
            };
            var outer = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 252, 252)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 125, 120)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(15),
                Padding = new Thickness(22)
            };
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = "Phát hiện bệnh nhân đã có lịch",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(21, 61, 73)),
                Margin = new Thickness(0, 0, 0, 12)
            });
            string message = "Bệnh nhân " + (row.PatientName ?? "").Trim() +
                " đã có lịch hẹn buổi " + (saved.Shift ?? "") +
                ", ngày " + saved.AppointmentDate.ToString("dd/MM/yyyy") + ".";
            panel.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 72, 79)),
                Margin = new Thickness(0, 0, 0, 20)
            });
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Action<Button> styleButton = b =>
            {
                b.Height = 40; b.MinWidth = 94; b.Padding = new Thickness(12, 7, 12, 7);
                b.Margin = new Thickness(0, 0, 8, 0);
                b.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(221, 239, 239));
                b.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(21, 61, 73));
                b.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(190, 220, 220));
            };
            var returnButton = new Button { Content = "Trả hẹn" }; styleButton(returnButton);
            returnButton.Click += (s, e) =>
            {
                try { Database.Return(saved.Id); LoadAppointments(); dialog.DialogResult = true; dialog.Close(); }
                catch (Exception ex) { ShowAppMessage(dialog, "Không thể trả hẹn: " + ex.Message); }
            };
            var changeButton = new Button { Content = "Đổi lịch" }; styleButton(changeButton);
            changeButton.Click += (s, e) =>
            {
                row.ExistingAppointmentId = saved.Id;
                row.ExistingAppointmentDate = saved.AppointmentDate.ToString("dd/MM/yyyy");
                row.ExistingShift = saved.Shift;
                dialog.Hide();
                bool changed = ChangeAppointmentFromScan(row, this);
                if (changed) LoadAppointments();
                dialog.Close();
            };
            var closeButton = new Button { Content = "Đóng", Margin = new Thickness(0) }; styleButton(closeButton);
            closeButton.Click += (s, e) => { dialog.DialogResult = true; dialog.Close(); };
            actions.Children.Add(returnButton); actions.Children.Add(changeButton); actions.Children.Add(closeButton);
            panel.Children.Add(actions); outer.Child = panel; dialog.Content = outer;
            dialog.ShowDialog();
        }

        private async void ScanHospitalList_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveBrowser == null || !ActiveBrowser.IsBrowserInitialized)
            {
                ShowAppMessage("Trình duyệt bệnh viện chưa sẵn sàng.", "Quét danh sách");
                return;
            }
            try
            {
                var frame = ActiveBrowser.GetMainFrame();
                if (frame == null || !frame.IsValid) { ShowAppMessage("Không đọc được trang hiện tại."); return; }
                var response = await frame.EvaluateScriptAsync(HospitalScanScript);
                if (!response.Success || response.Result == null)
                {
                    ShowAppMessage("Không thể đọc dữ liệu trang: " + response.Message, "Quét danh sách");
                    return;
                }
                var serializer = new JavaScriptSerializer();
                var payload = serializer.Deserialize<Dictionary<string, object>>(Convert.ToString(response.Result));
                if (payload.ContainsKey("error")) { ShowAppMessage(Convert.ToString(payload["error"]), "Quét danh sách"); return; }
                var rows = payload.ContainsKey("rows") ? payload["rows"] as System.Collections.ArrayList : null;
                if (rows == null || rows.Count == 0)
                {
                    string debug = payload.ContainsKey("debug") ? new JavaScriptSerializer().Serialize(payload["debug"]) : "Không có thông tin chẩn đoán";
                    ShowAppMessage("Đã tìm thấy bảng nhưng chưa đọc được dòng bệnh nhân.\n\nChi tiết: " + debug + "\n\nHãy gửi ảnh này cùng báo cáo DOM để điều chỉnh bộ đọc.", "Quét danh sách");
                    return;
                }
                var results = new List<HospitalScanRow>();
                foreach (var item in rows)
                {
                    var d = item as Dictionary<string, object>; if (d == null) continue;
                    string receipt = GetScanValue(d, "receiptCode");
                    string patient = GetScanValue(d, "patientCode");
                    if (string.IsNullOrWhiteSpace(receipt) || string.IsNullOrWhiteSpace(patient)) continue;
                    var match = Database.FindLatestAppointment(patient, receipt);
                    var r = new HospitalScanRow { ReceiptCode=receipt, PatientCode=patient,
                        PatientName=GetScanValue(d,"patientName"), Age=GetScanValue(d,"age"), Department=GetScanValue(d,"department"),
                        InjectionStatus=DetectInjectionStatus(GetScanValue(d,"serviceName")), OrderedAt=GetScanValue(d,"orderedAt"), ServiceName=GetScanValue(d,"serviceName"), Room=GetScanValue(d,"room") };
                    if (match != null) { r.Status = match.Status == "Đã hẹn" ? "ĐÃ CÓ LỊCH" : "ĐÃ TRẢ HẸN"; r.ExistingAppointmentId=match.Id; r.ExistingAppointmentDate=match.AppointmentDate.ToString("dd/MM/yyyy"); r.ExistingShift=match.Shift; }
                    else r.Status="CHƯA CÓ LỊCH";
                    results.Add(r);
                }
                if (results.Count == 0)
                {
                    ShowAppMessage("Đã đọc được bảng nhưng không có dòng nào có đủ Số phiếu và Mã BN. Hãy kiểm tra tiêu đề/cấu trúc hàng trong báo cáo DOM.", "Quét danh sách");
                    return;
                }
                int incompleteServices = results.Count(x => string.IsNullOrWhiteSpace(x.ServiceName) ||
                    x.ServiceName.TrimEnd('.', '…').Length <= 0 || x.ServiceName.EndsWith("...", StringComparison.Ordinal));
                // Chỉ cảnh báo khi người dùng chủ động bấm Quét DS.
                // Một lượt quét chỉ hiện một cảnh báo, các dòng trùng còn lại vẫn được đánh dấu trong bảng.
                var duplicate = results.FirstOrDefault(x => x.ExistingAppointmentId > 0);
                if (duplicate != null)
                {
                    var savedAppointment = Database.FindLatestAppointment(duplicate.PatientCode, duplicate.ReceiptCode);
                    if (savedAppointment != null)
                    {
                        ShowDuplicateNotice(duplicate, savedAppointment);
                        // Đã xử lý trực tiếp tại hộp thoại trùng lịch. Không mở thêm
                        // bảng kết quả quét, tránh buộc người dùng phải đóng lần thứ hai.
                        return;
                    }
                }
                ShowHospitalScanResults(results);
                if (incompleteServices > 0)
                    ShowAppMessage("Đã đọc " + results.Count + " dòng, nhưng có " + incompleteServices +
                        " dòng chưa lấy được tên dịch vụ đầy đủ. Vui lòng kiểm tra các dòng có dấu ... trước khi tạo lịch.",
                        "Cảnh báo dữ liệu chỉ định", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex) { ShowAppMessage("Lỗi khi quét danh sách bệnh nhân.\n\n" + ex.Message, "Quét danh sách", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private static string GetScanValue(Dictionary<string, object> data, string key)
        {
            object value; return data.TryGetValue(key, out value) && value != null ? Convert.ToString(value).Trim() : "";
        }

        private void ShowHospitalScanResults(List<HospitalScanRow> rows)
        {
            var window = new Window { Title="Kết quả quét chỉ định", Width=1250, Height=680, MinWidth=900, MinHeight=480,
                Owner=this, WindowStartupLocation=WindowStartupLocation.CenterOwner, Background=System.Windows.Media.Brushes.White };
            var root = new DockPanel { Margin=new Thickness(12) };
            var summary = new TextBlock { Text="Đã quét " + rows.Count + " dòng. Đối chiếu theo Mã BN + Số phiếu. Ca chưa xác định tiêm vẫn có thể tạo lịch hẹn.",
                TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,0,0,10), Foreground=System.Windows.Media.Brushes.DarkSlateGray };
            DockPanel.SetDock(summary,Dock.Top); root.Children.Add(summary);
            var grid = new DataGrid { ItemsSource=rows, AutoGenerateColumns=false, IsReadOnly=true, SelectionMode=DataGridSelectionMode.Single,
                CanUserAddRows=false, CanUserDeleteRows=false, HeadersVisibility=DataGridHeadersVisibility.Column };
            grid.Columns.Add(new DataGridTextColumn { Header="Kết quả", Binding=new System.Windows.Data.Binding("Status"), Width=105 });
            grid.Columns.Add(new DataGridTextColumn { Header="Số phiếu / Mã chỉ định", Binding=new System.Windows.Data.Binding("ReceiptCode"), Width=145 });
            grid.Columns.Add(new DataGridTextColumn { Header="Tuổi", Binding=new System.Windows.Data.Binding("Age"), Width=55 });
            grid.Columns.Add(new DataGridTextColumn { Header="Tiêm cản quang", Binding=new System.Windows.Data.Binding("InjectionStatus"), Width=105 });
            grid.Columns.Add(new DataGridTextColumn { Header="Mã BN", Binding=new System.Windows.Data.Binding("PatientCode"), Width=110 });
            grid.Columns.Add(new DataGridTextColumn { Header="Tên bệnh nhân", Binding=new System.Windows.Data.Binding("PatientName"), Width=160 });
            grid.Columns.Add(new DataGridTextColumn { Header="Khoa/phòng", Binding=new System.Windows.Data.Binding("Department"), Width=145 });
            grid.Columns.Add(new DataGridTextColumn { Header="Thời gian chỉ định", Binding=new System.Windows.Data.Binding("OrderedAt"), Width=140 });
            var serviceColumn = new DataGridTextColumn { Header="Dịch vụ (toàn bộ)", Binding=new System.Windows.Data.Binding("ServiceName"), Width=260 };
            serviceColumn.ElementStyle = new Style(typeof(TextBlock));
            serviceColumn.ElementStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            serviceColumn.ElementStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new System.Windows.Data.Binding("ServiceName")));
            grid.Columns.Add(serviceColumn);
            grid.Columns.Add(new DataGridTextColumn { Header="Lịch đã lưu", Binding=new System.Windows.Data.Binding("ExistingAppointmentDate"), Width=95 });
            grid.Columns.Add(new DataGridTextColumn { Header="Buổi", Binding=new System.Windows.Data.Binding("ExistingShift"), Width=65 });
            if (rows.Count == 1) grid.SelectedIndex = 0;

            var buttons = new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Right, Margin=new Thickness(0,10,0,0) };
            var returnButton = new Button { Content="Trả hẹn lịch cũ", MinWidth=135, Height=42, Padding=new Thickness(12,8,12,8), Margin=new Thickness(0,0,8,0) };
            returnButton.Click += (s,e) => {
                var selected=grid.SelectedItem as HospitalScanRow ?? (rows.Count == 1 ? rows[0] : null);
                if (selected==null || selected.ExistingAppointmentId<=0) { ShowAppMessage(window,"Chọn một dòng đang có lịch hẹn trước."); return; }
                try { Database.Return(selected.ExistingAppointmentId); LoadAppointments(); window.Close(); }
                catch(Exception ex) { ShowAppMessage(window,"Không thể trả hẹn: " + ex.Message); }
            };
            var changeButton = new Button { Content="Đổi lịch", MinWidth=100, Height=42, Padding=new Thickness(12,8,12,8), Margin=new Thickness(0,0,8,0) };
            changeButton.Click += (s,e) => {
                var selected=grid.SelectedItem as HospitalScanRow ?? (rows.Count == 1 ? rows[0] : null);
                if(selected==null || selected.ExistingAppointmentId<=0 || selected.Status=="ĐÃ TRẢ HẸN") { ShowAppMessage(window,"Chỉ bệnh nhân đang có lịch hẹn mới được đổi lịch."); return; }
                if (ChangeAppointmentFromScan(selected, window)) {
                    // Đọc lại lịch vừa cập nhật để hiển thị ngày/buổi mới trong bảng quét.
                    var updated=Database.FindActiveAppointment(selected.PatientCode,selected.ReceiptCode);
                    if(updated!=null){selected.ExistingAppointmentDate=updated.AppointmentDate.ToString("dd/MM/yyyy");selected.ExistingShift=updated.Shift;}
                    LoadAppointments(); window.Close();
                }
            };
            var createButton = new Button { Content="Tạo lịch hẹn", MinWidth=115, Height=42, Padding=new Thickness(12,8,12,8), Margin=new Thickness(0,0,8,0) };
            createButton.Click += (s,e) => {
                var selected=grid.SelectedItem as HospitalScanRow ?? (rows.Count == 1 ? rows[0] : null);
                if(selected==null){ShowAppMessage(window,"Hãy chọn một bệnh nhân.");return;}
                if(selected.ExistingAppointmentId>0 && selected.Status!="ĐÃ TRẢ HẸN"){ShowAppMessage(window,"Bệnh nhân đang có lịch hẹn. Hãy dùng nút Đổi lịch hoặc Trả hẹn lịch cũ.");return;}
                if (CreateAppointmentFromScan(selected, window)) {
                    selected.Status="ĐÃ CÓ LỊCH";
                    var match=Database.FindActiveAppointment(selected.PatientCode,selected.ReceiptCode);
                    if(match!=null){selected.ExistingAppointmentId=match.Id;selected.ExistingAppointmentDate=match.AppointmentDate.ToString("dd/MM/yyyy");selected.ExistingShift=match.Shift;}
                    grid.Items.Refresh(); LoadAppointments();
                    var successMessage = "Đã tạo lịch hẹn cho " + selected.PatientName + ".";
                    // Đóng hộp thoại quét trước, sau đó mới hiện thông báo.
                    // Tránh Windows chuyển Focus sang ứng dụng khác khi cửa sổ modal kết thúc.
                    window.Close();
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        RestoreMainWindowFocus();
                        ShowTemporarySuccess(successMessage, this);
                    }), DispatcherPriority.Background);
                }
            };
            var closeButton = new Button { Content="Đóng", MinWidth=90, Height=42, Padding=new Thickness(12,8,12,8) }; closeButton.Click += (s,e)=>window.Close();
            buttons.Children.Add(returnButton); buttons.Children.Add(changeButton); buttons.Children.Add(createButton); buttons.Children.Add(closeButton);
            DockPanel.SetDock(buttons,Dock.Bottom); root.Children.Add(buttons); root.Children.Add(grid); window.Content=root; window.ShowDialog();
        }

        private bool ChangeAppointmentFromScan(HospitalScanRow row, Window owner)
        {
            var date = new DatePicker { SelectedDate=DateTime.ParseExact(row.ExistingAppointmentDate,"dd/MM/yyyy",System.Globalization.CultureInfo.InvariantCulture), Height=36, Margin=new Thickness(0,0,0,14) };
            var shift = new ComboBox { Height=36, Margin=new Thickness(0,0,0,14) };
            shift.Items.Add("Sáng"); shift.Items.Add("Chiều"); shift.SelectedItem = row.ExistingShift == "Chiều" ? "Chiều" : "Sáng";
            return ShowScheduleDialog(row, owner, date, shift, true);
        }

        private bool ShowScheduleDialog(HospitalScanRow row, Window owner, DatePicker date, ComboBox shift, bool isChange)
        {
            var dialog = new Window { Title=isChange?"Đổi lịch hẹn":"Tạo lịch hẹn mới", Width=490, SizeToContent=SizeToContent.Height, MinHeight=330, MaxHeight=620,
                ResizeMode=ResizeMode.NoResize, Owner=owner, WindowStartupLocation=WindowStartupLocation.CenterOwner, Background=System.Windows.Media.Brushes.White };
            var panel = new StackPanel { Margin=new Thickness(22), Width=440 };
            panel.Children.Add(new TextBlock { Text=row.PatientName + "  |  " + row.PatientCode, FontSize=17, FontWeight=FontWeights.Bold, TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,0,0,8) });
            panel.Children.Add(new TextBlock { Text="Số phiếu: " + row.ReceiptCode + "\nDịch vụ: " + row.ServiceName, TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,0,0,12) });
            if(row.InjectionStatus=="Chưa xác định") panel.Children.Add(new TextBlock { Text="⚠ Chưa xác định được trạng thái tiêm. Bạn vẫn có thể tạo lịch; trạng thái này sẽ được lưu để kiểm tra sau.", Foreground=System.Windows.Media.Brushes.DarkOrange, TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,0,0,12) });
            panel.Children.Add(new TextBlock { Text="Ngày hẹn", Margin=new Thickness(0,0,0,4) }); panel.Children.Add(CreateScheduleDateControl(date));
            panel.Children.Add(new TextBlock { Text="Buổi hẹn", Margin=new Thickness(0,0,0,4) }); panel.Children.Add(shift);
            panel.Children.Add(new TextBlock { Text=isChange?"Chỉ cập nhật ngày và buổi của lịch hiện tại.":"Chỉ cần chọn ngày hẹn và buổi hẹn. Thông tin bệnh nhân và chỉ định sẽ được tự điền.", TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,0,0,16), Foreground=System.Windows.Media.Brushes.DimGray });
            var actions = new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center, Margin=new Thickness(0,4,0,0) };
            var save = new Button { Content=isChange?"Lưu đổi lịch":"Tạo lịch hẹn", MinWidth=140, Height=44, Padding=new Thickness(14,9,14,9), IsDefault=true, Margin=new Thickness(0,0,10,0) };
            var cancel = new Button { Content="Hủy", MinWidth=100, Height=44, Padding=new Thickness(14,9,14,9), IsCancel=true };
            bool saved=false;
            save.Click += (s,e) => {
                if(date.SelectedDate==null){ShowAppMessage(dialog,"Hãy chọn ngày hẹn.");return;}
                try {
                    if(isChange) Database.UpdateAppointmentSchedule(row.ExistingAppointmentId,date.SelectedDate.Value.Date,Convert.ToString(shift.SelectedItem));
                    else {
                        if(Database.FindActiveAppointment(row.PatientCode,row.ReceiptCode)!=null){ShowAppMessage(dialog,"Lịch này vừa được tạo ở nơi khác hoặc đã tồn tại. Không tạo trùng.");return;}
                        Database.Add(new Appointment { PatientCode=row.PatientCode, PatientName=row.PatientName, Department=row.Department, ExamCode=row.ReceiptCode, ServiceName=row.ServiceName, InjectionStatus=row.InjectionStatus, AppointmentDate=date.SelectedDate.Value.Date, Shift=Convert.ToString(shift.SelectedItem), Status="Đã hẹn", CreatedAt=DateTime.Now });
                    }
                    saved=true; dialog.DialogResult=true; dialog.Close();
                } catch(Exception ex){ShowAppMessage(dialog,(isChange?"Không thể đổi lịch: ":"Không thể tạo lịch: ") + ex.Message);}
            };
            actions.Children.Add(save); actions.Children.Add(cancel); panel.Children.Add(actions); dialog.Content=panel;
            return dialog.ShowDialog()==true && saved;
        }

        private static DockPanel CreateScheduleDateControl(DatePicker date)
        {
            var panel = new DockPanel { LastChildFill = true, Margin = new Thickness(0,0,0,14) };
            var minus = new Button { Content = "−", Width = 34, Height = 36, Margin = new Thickness(0,0,5,0), Padding = new Thickness(0), ToolTip = "Lùi một ngày" };
            var plus = new Button { Content = "+", Width = 34, Height = 36, Margin = new Thickness(5,0,0,0), Padding = new Thickness(0), ToolTip = "Tiến một ngày" };
            minus.Click += (s,e) => date.SelectedDate = (date.SelectedDate ?? DateTime.Today).AddDays(-1);
            plus.Click += (s,e) => date.SelectedDate = (date.SelectedDate ?? DateTime.Today).AddDays(1);
            DockPanel.SetDock(minus, Dock.Left); DockPanel.SetDock(plus, Dock.Right);
            date.Height = 36; date.Margin = new Thickness(0);
            panel.Children.Add(minus); panel.Children.Add(plus); panel.Children.Add(date);
            return panel;
        }

        private bool CreateAppointmentFromScan(HospitalScanRow row, Window owner)
        {
            if (string.IsNullOrWhiteSpace(row.PatientCode) || string.IsNullOrWhiteSpace(row.ReceiptCode) || string.IsNullOrWhiteSpace(row.PatientName))
            { ShowAppMessage(owner, "Thiếu mã BN, số phiếu hoặc tên bệnh nhân. Không thể tạo lịch."); return false; }
            var date = new DatePicker { SelectedDate=DateTime.Today.AddDays(1), Height=36, Margin=new Thickness(0,0,0,14) };
            var shift = new ComboBox { Height=36, SelectedIndex=0, Margin=new Thickness(0,0,0,14) }; shift.Items.Add("Sáng"); shift.Items.Add("Chiều");
            bool result=ShowScheduleDialog(row,owner,date,shift,false);
            return result;
        }

        private static string DetectInjectionStatus(string serviceName)
        {
            string value = (serviceName ?? "").ToLowerInvariant();
            string[] noTerms = { "không tiêm", "khong tiem", "không dùng thuốc cản quang", "khong dung thuoc can quang", "không sử dụng thuốc cản quang", "khong su dung thuoc can quang" };
            foreach (string term in noTerms) if (value.Contains(term)) return "Không tiêm";
            string[] yesTerms = { "có tiêm", "co tiem", "tiêm thuốc cản quang", "tiem thuoc can quang", "cản quang", "can quang" };
            foreach (string term in yesTerms) if (value.Contains(term)) return "Có tiêm";
            return "Chưa xác định";
        }

        // Chỉ đọc cấu trúc bảng/cột, không đọc nội dung từng bệnh nhân.
        private const string DomInspectionScript = @"
(function () {
    function clean(value) {
        return (value || '').replace(/\s+/g, ' ').trim();
    }

    function visible(element) {
        if (!element) return false;
        var style = window.getComputedStyle(element);
        return style.display !== 'none' &&
               style.visibility !== 'hidden' &&
               element.getClientRects().length > 0;
    }

    var selectors = [
        'table',
        '[role=""grid""]',
        '[role=""table""]',
        '[role=""treegrid""]',
        '.ant-table',
        '.ag-root',
        '.dataTable',
        '.table-responsive'
    ];

    var found = [];
    var seen = [];

    selectors.forEach(function (selector) {
        var elements = document.querySelectorAll(selector);
        for (var i = 0; i < elements.length; i++) {
            var element = elements[i];
            if (!visible(element) || seen.indexOf(element) >= 0) continue;
            seen.push(element);

            var headers = [];
            var headerNodes = element.querySelectorAll(
                'thead th, [role=""columnheader""], th'
            );

            for (var h = 0; h < headerNodes.length && h < 30; h++) {
                var headerText = clean(headerNodes[h].innerText ||
                                       headerNodes[h].textContent);
                if (headerText && headers.indexOf(headerText) < 0)
                    headers.push(headerText);
            }

            if (headers.length === 0) {
                var firstRow = element.querySelector('tr');
                if (firstRow) {
                    var cells = firstRow.querySelectorAll('th, td');
                    for (var c = 0; c < cells.length && c < 30; c++) {
                        var cellText = clean(cells[c].innerText ||
                                              cells[c].textContent);
                        if (cellText) headers.push(cellText);
                    }
                }
            }

            var rowCount = element.querySelectorAll(
                'tbody tr, [role=""row""]'
            ).length;

            if (rowCount === 0)
                rowCount = element.querySelectorAll('tr').length;

            var id = element.id ? '#' + element.id : '';
            var className = '';
            if (typeof element.className === 'string' && element.className)
                className = '.' + element.className.trim().replace(/\s+/g, '.');

            found.push({
                selector: selector,
                tag: element.tagName.toLowerCase(),
                id: id,
                className: className,
                rows: rowCount,
                headers: headers
            });
        }
    });

    var lines = [];
    lines.push('KIỂM TRA CẤU TRÚC DOM');
    lines.push('Tên miền: ' + location.origin);
    lines.push('Số bảng/vùng dữ liệu tìm thấy: ' + found.length);
    lines.push('');

    if (found.length === 0) {
        lines.push('Không tìm thấy bảng HTML hoặc vùng dữ liệu theo các selector phổ biến.');
        lines.push('Website có thể dựng bảng bằng canvas hoặc cấu trúc DOM riêng.');
    }

    found.forEach(function (item, index) {
        lines.push('VÙNG ' + (index + 1));
        lines.push('Phần tử: ' + item.tag);
        lines.push('Selector dò được: ' + item.selector);
        lines.push('ID: ' + (item.id || '(không có)'));
        lines.push('Class: ' + (item.className || '(không có)'));
        lines.push('Số dòng: ' + item.rows);
        lines.push('Tiêu đề cột:');
        if (item.headers.length === 0) {
            lines.push('  (chưa đọc được tiêu đề)');
        } else {
            item.headers.forEach(function (header, headerIndex) {
                lines.push('  ' + (headerIndex + 1) + '. ' + header);
            });
        }
        lines.push('');
    });

    lines.push('LƯU Ý: Công cụ chỉ lấy cấu trúc và tiêu đề cột, không lấy mã BN, họ tên hay dữ liệu từng dòng.');
    return lines.join('\n');
})()";

        private async Task InspectBrowserDomAsync(
            ChromiumWebBrowser browser,
            string browserName)
        {
            if (browser == null || !browser.IsBrowserInitialized)
            {
                ShowAppMessage(
                    "Trình duyệt chưa khởi tạo xong. Hãy mở website và thử lại.",
                    "Chưa sẵn sàng",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                var frame = browser.GetMainFrame();
                if (frame == null || !frame.IsValid)
                {
                    ShowAppMessage("Không truy cập được nội dung trang hiện tại.");
                    return;
                }

                JavascriptResponse response =
                    await frame.EvaluateScriptAsync(DomInspectionScript);

                if (!response.Success)
                {
                    ShowAppMessage(
                        "Không thể kiểm tra DOM.\n" + response.Message,
                        "Lỗi đọc trang",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                string report = response.Result as string;
                if (string.IsNullOrWhiteSpace(report))
                    report = "Website không trả về thông tin cấu trúc DOM.";

                ShowDomReport(browserName, report);
            }
            catch (Exception ex)
            {
                ShowAppMessage(
                    "Không thể kiểm tra cấu trúc website.\n\n" + ex.Message,
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ShowDomReport(string browserName, string report)
        {
            var window = new Window
            {
                Title = "Kết quả kiểm tra DOM - " + browserName,
                Width = 760,
                Height = 620,
                MinWidth = 520,
                MinHeight = 400,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = System.Windows.Media.Brushes.White
            };

            var root = new DockPanel { Margin = new Thickness(12) };

            var note = new TextBlock
            {
                Text = "Hãy sao chép toàn bộ kết quả và gửi lại để cấu hình bộ đọc chỉ định. Báo cáo không chứa dữ liệu từng bệnh nhân.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = System.Windows.Media.Brushes.DarkSlateGray
            };
            DockPanel.SetDock(note, Dock.Top);
            root.Children.Add(note);

            var output = new TextBox
            {
                Text = report,
                IsReadOnly = true,
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12
            };
            root.Children.Add(output);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var copyButton = new Button
            {
                Content = "Sao chép báo cáo",
                Width = 135,
                Height = 34,
                Margin = new Thickness(0, 0, 8, 0)
            };
            copyButton.Click += (s, e) =>
            {
                Clipboard.SetText(report);
                ShowAppMessage(window, "Đã sao chép báo cáo DOM.");
            };

            var closeButton = new Button
            {
                Content = "Đóng",
                Width = 85,
                Height = 34
            };
            closeButton.Click += (s, e) => window.Close();

            buttons.Children.Add(copyButton);
            buttons.Children.Add(closeButton);
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(buttons);

            window.Content = root;
            window.ShowDialog();
        }

        private async void InspectDom_Click(object sender, RoutedEventArgs e)
        {
            await InspectBrowserDomAsync(ActiveBrowser, "Trình duyệt bệnh viện");
        }

        private async void InspectAddDom_Click(object sender, RoutedEventArgs e)
        {
            await InspectBrowserDomAsync(ActiveBrowser, "Trình duyệt thêm lịch");
        }

        private void AddAppointment_Click(object sender, RoutedEventArgs e)
        {
            string code = (PatientCodeBox.Text ?? "").Trim();
            string name = (PatientNameBox.Text ?? "").Trim();
            string department = (DepartmentBox.Text ?? "").Trim();
            string exam = (ExamCodeBox.Text ?? "").Trim();
            string service = (ServiceBox.Text ?? "").Trim();
            var injectionItem = InjectionStatusBox.SelectedItem as ComboBoxItem;
            string injectionStatus = injectionItem == null ? "Không tiêm" : injectionItem.Content.ToString();

            if (code.Length == 0 ||
                name.Length == 0 ||
                department.Length == 0 ||
                AppointmentDatePicker.SelectedDate == null ||
                ShiftBox.SelectedItem == null)
            {
                ShowAppMessage(
                    "Vui lòng nhập đầy đủ mã bệnh nhân, tên bệnh nhân, khoa phòng, ngày hẹn và buổi hẹn.",
                    "Thiếu thông tin",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (Database.HasActiveAppointment(code, exam))
            {
                ShowAppMessage(
                    "Bệnh nhân đã có lịch hẹn đang hiệu lực. Vui lòng kiểm tra lịch cũ trước khi thêm lịch mới.",
                    "Phát hiện lịch trùng",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var selectedShift = ShiftBox.SelectedItem as ComboBoxItem;
            if (selectedShift == null) return;

            try
            {
                Database.Add(new Appointment
                {
                    PatientCode = code,
                    PatientName = name,
                    Department = department,
                    ExamCode = exam,
                    ServiceName = service,
                    InjectionStatus = injectionStatus,
                    AppointmentDate = AppointmentDatePicker.SelectedDate.Value,
                    Shift = selectedShift.Content.ToString()
                });

                ShowAppMessage("Đã thêm lịch hẹn thành công.");

                PatientCodeBox.Clear();
                PatientNameBox.Clear();
                DepartmentBox.Clear();
                ExamCodeBox.Clear();

                ServiceBox.Text = "Chụp CLVT";
                InjectionStatusBox.SelectedIndex = 2;
                AppointmentDatePicker.SelectedDate = DateTime.Today.AddDays(1);
                ShiftBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                ShowAppMessage("Không thể lưu lịch hẹn.\n\n" + ex.Message);
            }
        }

        private void AppointmentsGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            var row = e.Row.DataContext as DataRowView;
            if (row == null) return;
            bool returned = Convert.ToString(row["Trạng thái"]) == "Đã trả hẹn";
            e.Row.Foreground = returned ? System.Windows.Media.Brushes.Gray : System.Windows.Media.Brushes.DarkSlateGray;
            e.Row.Opacity = returned ? 0.72 : 1.0;
            // Không đặt Background/BorderBrush cục bộ ở đây: các DataTrigger của CellStyle
            // cần được quyền tô nền và viền cho hai cột trạng thái.
            foreach (var cell in FindVisualChildren<DataGridCell>(e.Row))
            {
                var text = cell.Content as TextBlock;
                if (text != null)
                    text.TextDecorations = returned ? TextDecorations.Strikethrough : null;
                string header = cell.Column == null ? "" : Convert.ToString(cell.Column.Header);
                if (header != "Buổi" && header != "Tiêm thuốc cản quang")
                    cell.Foreground = returned ? System.Windows.Media.Brushes.Gray : System.Windows.Media.Brushes.DarkSlateGray;
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T) yield return (T)child;
                foreach (T descendant in FindVisualChildren<T>(child)) yield return descendant;
            }
        }

        private void LoadAppointments()
        {
            DateTime from = FromDatePicker.SelectedDate ?? DateTime.Today;
            DateTime to = ToDatePicker.SelectedDate ?? from;

            if (to.Date < from.Date)
            {
                ResetAppointmentCounters();
                ShowAppMessage("Ngày đến phải bằng hoặc sau ngày bắt đầu.");
                return;
            }

            var shiftItem = FilterShiftBox.SelectedItem as ComboBoxItem;
            string shift = shiftItem == null ? "Cả ngày" : shiftItem.Content.ToString();

            var injectionItem = InjectionFilterBox.SelectedItem as ComboBoxItem;
            string injectionFilter = injectionItem == null
                ? "Tất cả"
                : injectionItem.Content.ToString();

            try
            {
                // Bộ đếm luôn dựa trên toàn bộ lịch trong khoảng ngày/buổi,
                // không bị ảnh hưởng bởi bộ lọc tiêm hoặc tùy chọn ẩn lịch đã trả.
                DataTable allData = Database.GetAppointments(
                    from.Date, to.Date, shift, true, "Tất cả");

                int totalCount = allData.Rows.Count;
                int totalInjectionCount = 0;
                int totalNoInjectionCount = 0;
                int activeCount = 0;
                int activeInjectionCount = 0;
                int activeNoInjectionCount = 0;

                foreach (DataRow row in allData.Rows)
                {
                    string category = GetInjectionCategory(row);
                    bool isActive = Convert.ToString(row["Trạng thái"]) == "Đã hẹn";

                    if (category == "Có tiêm")
                    {
                        totalInjectionCount++;
                        if (isActive)
                        {
                            activeCount++;
                            activeInjectionCount++;
                        }
                    }
                    else if (category == "Không tiêm")
                    {
                        totalNoInjectionCount++;
                        if (isActive)
                        {
                            activeCount++;
                            activeNoInjectionCount++;
                        }
                    }
                    else if (isActive)
                    {
                        // Ca chưa xác định loại tiêm vẫn thuộc tổng số ca còn hẹn.
                        activeCount++;
                    }
                }

                DataTable visibleData = Database.GetAppointments(
                    from.Date,
                    to.Date,
                    shift,
                    IncludeReturnedCheck.IsChecked == true,
                    injectionFilter);

                ConfigureAppointmentsGrid(visibleData);
                AppointmentsGrid.ItemsSource = visibleData.DefaultView;

                TotalCountText.Text = totalCount + " ca";
                TotalInjectionCountText.Text = totalInjectionCount.ToString();
                TotalNoInjectionCountText.Text = totalNoInjectionCount.ToString();
                ActiveCountText.Text = activeCount + " ca";
                ActiveInjectionCountText.Text = activeInjectionCount.ToString();
                ActiveNoInjectionCountText.Text = activeNoInjectionCount.ToString();
            }
            catch (Exception ex)
            {
                ShowAppMessage(
                    "Không thể tải danh sách lịch hẹn.\\n\\n" + ex.Message,
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static string GetInjectionCategory(DataRow row)
        {
            string explicitStatus = row.Table.Columns.Contains("Tiêm thuốc cản quang")
                ? Convert.ToString(row["Tiêm thuốc cản quang"]).Trim()
                : "";

            if (string.Equals(explicitStatus, "Có tiêm", StringComparison.OrdinalIgnoreCase))
                return "Có tiêm";
            if (string.Equals(explicitStatus, "Không tiêm", StringComparison.OrdinalIgnoreCase))
                return "Không tiêm";
            if (string.Equals(explicitStatus, "Chưa xác định", StringComparison.OrdinalIgnoreCase))
                return "Chưa xác định";

            string service = row.Table.Columns.Contains("Dịch vụ")
                ? Convert.ToString(row["Dịch vụ"]).ToLowerInvariant()
                : "";

            if (service.Contains("không tiêm") || service.Contains("khong tiem") ||
                service.Contains("không sử dụng thuốc cản quang") ||
                service.Contains("không dùng thuốc cản quang") ||
                service.Contains("khong su dung thuoc can quang"))
                return "Không tiêm";

            if (service.Contains("tiêm") || service.Contains("tiem"))
                return "Có tiêm";

            return "Chưa xác định";
        }

        private static void AddGridCellTrigger(Style style, string value, System.Windows.Media.Color background, System.Windows.Media.Color border, System.Windows.Media.Color foreground)
        {
            var trigger = new DataTrigger { Binding = new System.Windows.Data.Binding("[Tiêm thuốc cản quang]"), Value = value };
            trigger.Setters.Add(new Setter(Control.BackgroundProperty, new System.Windows.Media.SolidColorBrush(background)));
            trigger.Setters.Add(new Setter(Control.BorderBrushProperty, new System.Windows.Media.SolidColorBrush(border)));
            trigger.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            trigger.Setters.Add(new Setter(Control.ForegroundProperty, new System.Windows.Media.SolidColorBrush(foreground)));
            style.Triggers.Add(trigger);
        }

        private void ConfigureAppointmentsGrid(DataTable data)
        {
            AppointmentsGrid.AutoGenerateColumns = false;
            AppointmentsGrid.Columns.Clear();
            // Khôi phục cỡ chữ gọn của v0.5.8 để phù hợp màn hình độ phân giải thấp.
            AppointmentsGrid.FontSize = 13.5;
            AppointmentsGrid.RowHeight = 34;
            AppointmentsGrid.ColumnHeaderHeight = 36;
            AppointmentsGrid.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
            AppointmentsGrid.HorizontalGridLinesBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(225, 233, 236));
            AppointmentsGrid.VerticalGridLinesBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(238, 242, 244));
            AppointmentsGrid.RowBackground = System.Windows.Media.Brushes.White;
            AppointmentsGrid.AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 251, 252));

            string[] order = { "Id", "Mã BN", "Tên bệnh nhân", "Khoa phòng", "Mã chỉ định", "Dịch vụ", "Tiêm thuốc cản quang", "Ngày hẹn", "Buổi", "Trạng thái", "Ngày tạo lịch" };
            foreach (string name in order)
            {
                if (!data.Columns.Contains(name)) continue;
                double width = 78;
                if (name == "Id") width = 32;
                else if (name == "Mã BN") width = 68;
                else if (name == "Tên bệnh nhân") width = 70;
                else if (name == "Khoa phòng") width = 65;
                else if (name == "Mã chỉ định") width = 60;
                else if (name == "Dịch vụ") width = 75;
                else if (name == "Tiêm thuốc cản quang") width = 78;
                else if (name == "Ngày hẹn") width = 68;
                else if (name == "Buổi") width = 42;
                else if (name == "Trạng thái") width = 60;
                else if (name == "Ngày tạo lịch") width = 80;

                bool leftAligned = name == "Mã BN" || name == "Tên bệnh nhân" || name == "Khoa phòng" || name == "Dịch vụ";
                var alignment = leftAligned ? TextAlignment.Left : TextAlignment.Center;

                var textStyle = new Style(typeof(TextBlock));
                textStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 13.5));
                textStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
                textStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
                textStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
                textStyle.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(4, 0, 4, 0)));
                textStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, alignment));

                var headerStyle = new Style(typeof(DataGridColumnHeader));
                headerStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,
                    leftAligned ? HorizontalAlignment.Left : HorizontalAlignment.Center));
                headerStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
                headerStyle.Setters.Add(new Setter(Control.FontSizeProperty, 12.5));
                headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(3, 5, 3, 5)));

                var cellStyle = new Style(typeof(DataGridCell));
                cellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(3,1,3,1)));
                cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
                if (name == "Buổi")
                {
                    var morning = new DataTrigger { Binding = new System.Windows.Data.Binding("[Buổi]"), Value = "Sáng" };
                    morning.Setters.Add(new Setter(Control.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255,229,153))));
                    morning.Setters.Add(new Setter(Control.BorderBrushProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(210,155,35))));
                    morning.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
                    morning.Setters.Add(new Setter(Control.ForegroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(125,83,0))));
                    cellStyle.Triggers.Add(morning);
                    var afternoon = new DataTrigger { Binding = new System.Windows.Data.Binding("[Buổi]"), Value = "Chiều" };
                    afternoon.Setters.Add(new Setter(Control.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(190,220,255))));
                    afternoon.Setters.Add(new Setter(Control.BorderBrushProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(75,135,205))));
                    afternoon.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
                    afternoon.Setters.Add(new Setter(Control.ForegroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(28,83,145))));
                    cellStyle.Triggers.Add(afternoon);
                }
                else if (name == "Tiêm thuốc cản quang")
                {
                    AddGridCellTrigger(cellStyle, "Có tiêm", System.Windows.Media.Color.FromRgb(210,242,218), System.Windows.Media.Color.FromRgb(75,170,105), System.Windows.Media.Color.FromRgb(24,112,55));
                    AddGridCellTrigger(cellStyle, "Không tiêm", System.Windows.Media.Color.FromRgb(232,237,242), System.Windows.Media.Color.FromRgb(145,158,170), System.Windows.Media.Color.FromRgb(75,87,96));
                    AddGridCellTrigger(cellStyle, "Chưa xác định", System.Windows.Media.Color.FromRgb(255,229,199), System.Windows.Media.Color.FromRgb(220,145,65), System.Windows.Media.Color.FromRgb(150,83,12));
                }
                var column = new DataGridTextColumn
                {
                    Header = name,
                    Binding = new System.Windows.Data.Binding { Path = new PropertyPath("[" + name + "]") },
                    Width = (name == "Tên bệnh nhân" || name == "Khoa phòng" || name == "Dịch vụ")
                        ? new DataGridLength(1, DataGridLengthUnitType.Star)
                        : new DataGridLength(width),
                    MinWidth = width,
                    ElementStyle = textStyle,
                    HeaderStyle = headerStyle,
                    CellStyle = cellStyle,
                    SortMemberPath = name
                };
                AppointmentsGrid.Columns.Add(column);
            }
        }

        private void AppointmentsGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            try
            {
                var view = AppointmentsGrid.ItemsSource as DataView;
                if (view == null) return;
                string header = Convert.ToString(e.Column.Header);
                string sortColumn = header;
                if (header == "Ngày hẹn") sortColumn = "AppointmentDateSort";
                else if (header == "Tiêm thuốc cản quang") sortColumn = "InjectionSort";
                else if (header == "Ngày tạo lịch") sortColumn = "CreatedAtSort";
                ListSortDirection direction = e.Column.SortDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending : ListSortDirection.Ascending;
                view.Sort = "[" + sortColumn.Replace("]", "]]" ) + "] " + (direction == ListSortDirection.Ascending ? "ASC" : "DESC");
                foreach (var column in AppointmentsGrid.Columns) column.SortDirection = null;
                e.Column.SortDirection = direction;
            }
            catch (Exception ex)
            {
                ShowAppMessage("Không thể sắp xếp cột này. Danh sách vẫn được giữ nguyên.\n\n" + ex.Message,
                    "Sắp xếp", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ResetAppointmentCounters()
        {
            TotalCountText.Text = "0 ca";
            TotalInjectionCountText.Text = "0";
            TotalNoInjectionCountText.Text = "0";
            ActiveCountText.Text = "0 ca";
            ActiveInjectionCountText.Text = "0";
            ActiveNoInjectionCountText.Text = "0";
            AppointmentsGrid.ItemsSource = null;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadAppointments();
        }

        private void AdjustAppointmentDate(int days)
        {
            AppointmentDatePicker.SelectedDate = (AppointmentDatePicker.SelectedDate ?? DateTime.Today).AddDays(days);
        }

        private void AppointmentDateMinus_Click(object sender, RoutedEventArgs e) { AdjustAppointmentDate(-1); }
        private void AppointmentDatePlus_Click(object sender, RoutedEventArgs e) { AdjustAppointmentDate(1); }

        private void AdjustFilterDate(DatePicker picker, int days, bool isFrom)
        {
            DateTime current = picker.SelectedDate ?? DateTime.Today;
            DateTime next = current.AddDays(days);
            if (isFrom && ToDatePicker.SelectedDate.HasValue && next.Date > ToDatePicker.SelectedDate.Value.Date)
                ToDatePicker.SelectedDate = next.Date;
            if (!isFrom && FromDatePicker.SelectedDate.HasValue && next.Date < FromDatePicker.SelectedDate.Value.Date)
                FromDatePicker.SelectedDate = next.Date;
            picker.SelectedDate = next.Date;
            LoadAppointments();
        }

        private void FromDateMinus_Click(object sender, RoutedEventArgs e) { AdjustFilterDate(FromDatePicker, -1, true); }
        private void FromDatePlus_Click(object sender, RoutedEventArgs e) { AdjustFilterDate(FromDatePicker, 1, true); }
        private void ToDateMinus_Click(object sender, RoutedEventArgs e) { AdjustFilterDate(ToDatePicker, -1, false); }
        private void ToDatePlus_Click(object sender, RoutedEventArgs e) { AdjustFilterDate(ToDatePicker, 1, false); }

        private void FilterSelection_Changed(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (IsLoaded &&
                FilterShiftBox.SelectedItem != null &&
                InjectionFilterBox.SelectedItem != null)
                LoadAppointments();
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (IsLoaded &&
                FilterShiftBox.SelectedItem != null &&
                InjectionFilterBox.SelectedItem != null)
                LoadAppointments();
        }

        private void ReturnAppointment_Click(object sender, RoutedEventArgs e)
        {
            var view = AppointmentsGrid.SelectedItem as DataRowView;

            if (view == null)
            {
                ShowAppMessage("Hãy chọn một lịch trong danh sách.");
                return;
            }

            if (Convert.ToString(view["Trạng thái"]) != "Đã hẹn")
            {
                ShowAppMessage("Lịch này đã được trả trước đó hoặc không còn hiệu lực.");
                return;
            }

            string patientName = Convert.ToString(view["Tên bệnh nhân"]);
            string patientCode = Convert.ToString(view["Mã BN"]);

            if (ShowAppMessage(
                "Xác nhận trả lịch của bệnh nhân:\n\n" +
                patientName + "\nMã BN: " + patientCode +
                "\n\nLịch vẫn được lưu trong lịch sử.",
                "Xác nhận trả lịch",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                Database.Return(Convert.ToInt64(view["Id"]));
                LoadAppointments();
                ShowAppMessage("Đã trả lịch thành công.");
            }
            catch (Exception ex)
            {
                ShowAppMessage("Không thể trả lịch.\n\n" + ex.Message);
            }
        }
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private void RestoreMainWindowFocus()
        {
            try
            {
                if (WindowState == WindowState.Minimized)
                    WindowState = WindowState.Normal;

                Show();
                Activate();
                Focus();

                var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (handle != IntPtr.Zero)
                {
                    ShowWindow(handle, 9); // SW_RESTORE
                    SetForegroundWindow(handle);
                }
            }
            catch
            {
                // Không để lỗi khôi phục Focus làm gián đoạn thao tác tạo lịch.
            }
        }

        private void ShowTemporarySuccess(string message, Window owner = null)
        {
            // Thong bao thanh cong dang gon, tu dong dong sau 1 giay.
            var notice = new Window
            {
                Title = "Thành công",
                Width = 360,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                ShowInTaskbar = false,
                Owner = owner ?? this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Topmost = true
            };

            var border = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(245, 252, 250)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(20, 145, 130)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(22)
            };

            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = "Đã tạo lịch hẹn",
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(12, 65, 70)),
                Margin = new Thickness(0, 0, 0, 8)
            });
            panel.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(45, 65, 68))
            });
            border.Child = panel;
            notice.Content = border;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                notice.Close();
            };
            notice.Closed += (s, e) =>
            {
                timer.Stop();
                // Khi thông báo tự đóng, trả Focus về cửa sổ SoHenCT.
                Dispatcher.BeginInvoke(new Action(RestoreMainWindowFocus), DispatcherPriority.Background);
            };
            notice.Show();
            timer.Start();
        }

        private static MessageBoxResult ShowAppMessage(string message)
        { return ShowAppMessage(null, message, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information); }
        private static MessageBoxResult ShowAppMessage(string message, string caption)
        { return ShowAppMessage(null, message, caption, MessageBoxButton.OK, MessageBoxImage.Information); }
        private static MessageBoxResult ShowAppMessage(string message, string caption, MessageBoxButton buttons, MessageBoxImage image)
        { return ShowAppMessage(null, message, caption, buttons, image); }
        private static MessageBoxResult ShowAppMessage(Window owner, string message)
        { return ShowAppMessage(owner, message, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information); }
        private static MessageBoxResult ShowAppMessage(Window owner, string message, string caption)
        { return ShowAppMessage(owner, message, caption, MessageBoxButton.OK, MessageBoxImage.Information); }
        private static MessageBoxResult ShowAppMessage(Window owner, string message, string caption, MessageBoxButton buttons, MessageBoxImage image)
        {
            var dialog = new Window { Title=caption, Width=440, SizeToContent=SizeToContent.Height, MinHeight=170, MaxHeight=520,
                ResizeMode=ResizeMode.NoResize, WindowStartupLocation=owner==null?WindowStartupLocation.CenterScreen:WindowStartupLocation.CenterOwner,
                Owner=owner, Background=System.Windows.Media.Brushes.Transparent, WindowStyle=WindowStyle.None, AllowsTransparency=true, ShowInTaskbar=false };
            var outer = new Border { Background=System.Windows.Media.Brushes.White, BorderBrush=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(207,226,228)), BorderThickness=new Thickness(1), CornerRadius=new CornerRadius(16), Padding=new Thickness(22) };
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text=caption, FontSize=17, FontWeight=FontWeights.Bold, Margin=new Thickness(0,0,0,12), Foreground=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(24,61,72)) });
            panel.Children.Add(new TextBlock { Text=message??"", TextWrapping=TextWrapping.Wrap, FontSize=13, Margin=new Thickness(0,0,0,20), Foreground=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45,72,79)) });
            var buttonsPanel = new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Right };
            MessageBoxResult result=MessageBoxResult.None;
            Action<string,MessageBoxResult> add=(label,value)=>{var b=new Button{Content=label,MinWidth=88,Height=40,Padding=new Thickness(14,8,14,8),Margin=new Thickness(0,0,8,0),IsDefault=value==MessageBoxResult.OK||value==MessageBoxResult.Yes};b.Click+=(s,e)=>{result=value;dialog.DialogResult=true;dialog.Close();};buttonsPanel.Children.Add(b);};
            if(buttons==MessageBoxButton.YesNo || buttons==MessageBoxButton.YesNoCancel){add("Có",MessageBoxResult.Yes);add("Không",MessageBoxResult.No);if(buttons==MessageBoxButton.YesNoCancel)add("Hủy",MessageBoxResult.Cancel);}
            else if(buttons==MessageBoxButton.OKCancel){add("OK",MessageBoxResult.OK);add("Hủy",MessageBoxResult.Cancel);}
            else add("Đóng",MessageBoxResult.OK);
            panel.Children.Add(buttonsPanel); outer.Child=panel; dialog.Content=outer; dialog.ShowDialog(); return result;
        }

    }

    // Chặn cửa sổ popup của Chromium và mở URL đích trong tab tích hợp.
    internal sealed class TabLifeSpanHandler : ILifeSpanHandler
    {
        private readonly Action<string> openInTab;

        public TabLifeSpanHandler(Action<string> openInTab)
        {
            this.openInTab = openInTab;
        }

        public bool OnBeforePopup(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame,
            string targetUrl, string targetFrameName, WindowOpenDisposition targetDisposition,
            bool userGesture, IPopupFeatures popupFeatures, IWindowInfo windowInfo,
            IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser newBrowser)
        {
            newBrowser = null;
            if (!string.IsNullOrWhiteSpace(targetUrl))
                openInTab(targetUrl);
            return true;
        }

        public void OnAfterCreated(IWebBrowser chromiumWebBrowser, IBrowser browser) { }
        public bool DoClose(IWebBrowser chromiumWebBrowser, IBrowser browser) { return false; }
        public void OnBeforeClose(IWebBrowser chromiumWebBrowser, IBrowser browser) { }
    }

}
