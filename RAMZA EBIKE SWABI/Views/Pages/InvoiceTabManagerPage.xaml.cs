using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Ramza_EBike_Swabi.Models;
using Ramza_EBike_Swabi.Services;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class InvoiceTabManagerPage : Page
    {
        private readonly MainLayout _layout;
        private readonly InvoiceDraftService _draftService = new();

        private int _tabCounter = 1;

        // ===========================
        // CONSTRUCTOR
        // ===========================
        private bool _initialized = false;

        public InvoiceTabManagerPage(MainLayout layout)
        {
            InitializeComponent();
            _layout = layout;
            Loaded += async (_, __) =>
            {
                if (_initialized) return;   // ✅ Only run once ever
                _initialized = true;
                await RestoreOrCreateTabsAsync();
            };
        }

        // ===========================
        // RESTORE DRAFTS OR CREATE FIRST TAB
        // ===========================
        private async Task RestoreOrCreateTabsAsync()
        {
            var drafts = await _draftService.GetAllDraftsAsync();

            if (drafts.Count == 0)
            {
                // No saved drafts — open one blank tab
                await AddNewInvoiceTabAsync();
            }
            else
            {
                // Restore each draft tab
                foreach (var draft in drafts.OrderBy(d => d.TabOrder))
                {
                    var data = InvoiceDraftService.Deserialize(draft.DraftJson)
                               ?? new InvoiceDraftData();

                    var invoicePage = new GenerateInvoicePage();
                    invoicePage.SetTabManager(this);
                    invoicePage.SetDraftId(draft.Id);
                    invoicePage.RestoreFromDraft(data);

                    // Update counter so new tabs don't duplicate names
                    _tabCounter = Math.Max(_tabCounter, draft.TabOrder + 2);

                    AddTab(draft.TabTitle, invoicePage);
                }
            }
        }

        // ===========================
        // ADD NEW BLANK TAB
        // ===========================
        public async Task AddNewInvoiceTabAsync()
        {
            string title = $"New Invoice {_tabCounter++}";
            int order = InvoiceTabs.Items.Count;
            int draftId = await _draftService.CreateDraftAsync(title, order);

            var invoicePage = new GenerateInvoicePage();
            invoicePage.SetTabManager(this);
            invoicePage.SetDraftId(draftId);

            AddTab(title, invoicePage);
        }

        // ===========================
        // ADD TAB (shared)
        // ===========================
        private void AddTab(string title, GenerateInvoicePage invoicePage)
        {
            var header = BuildTabHeader(title);
            var tabItem = new TabItem
            {
                Header = header,
                Content = new Frame
                {
                    Content = invoicePage,
                    NavigationUIVisibility =
                        System.Windows.Navigation.NavigationUIVisibility.Hidden
                },
                Tag = invoicePage
            };

            // Wire the X button
            if (header is StackPanel sp && sp.Children[1] is Button closeBtn)
                closeBtn.Click += async (_, __) => await CloseTabAsync(tabItem);

            InvoiceTabs.Items.Add(tabItem);
            InvoiceTabs.SelectedItem = tabItem;
        }

        // ===========================
        // BUILD TAB HEADER  "Title  x"
        // ===========================
        private static StackPanel BuildTabHeader(string title)
        {
            var tb = new TextBlock
            {
                Text = title,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                MaxWidth = 160,
                TextTrimming = System.Windows.TextTrimming.CharacterEllipsis
            };

            var btn = new Button
            {
                Content = "x",
                Width = 16,
                Height = 16,
                FontSize = 10,
                Padding = new Thickness(0),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Close tab"
            };

            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(tb);
            panel.Children.Add(btn);
            return panel;
        }

        // ===========================
        // CLOSE TAB
        // ===========================
        public async Task CloseTabAsync(TabItem tab)
        {
            if (tab.Tag is not GenerateInvoicePage page) return;

            if (InvoiceTabs.Items.Count == 1)
            {
                // Last tab — reset instead of close
                var result = MessageBox.Show(
                    "This is the last open invoice.\nDo you want to clear it?",
                    "Last Tab", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await page.ClearDraftAsync();
                    page.ResetFormPublic();
                    RenameTab(tab, $"New Invoice {_tabCounter++}");
                }
                return;
            }

            // Delete the draft from DB
            await page.ClearDraftAsync();
            InvoiceTabs.Items.Remove(tab);
        }

        // ===========================
        // RENAME TAB TITLE
        // ===========================
        public void RenameActiveTab(string newTitle)
        {
            if (InvoiceTabs.SelectedItem is TabItem tab)
                RenameTab(tab, newTitle);
        }

        private static void RenameTab(TabItem tab, string newTitle)
        {
            if (tab.Header is StackPanel sp && sp.Children[0] is TextBlock tb)
                tb.Text = newTitle;
        }

        // ===========================
        // OPEN EXISTING INVOICE IN NEW TAB (called from SearchInvoicePage)
        // ===========================
        public async Task OpenInvoiceForEditAsync(CustomerInvoice invoice)
        {
            string title = $"Edit: {invoice.Customer?.Name ?? $"INV-{invoice.CustomerInvoiceId}"}";
            int order = InvoiceTabs.Items.Count;
            int draftId = await _draftService.CreateDraftAsync(title, order);

            var invoicePage = new GenerateInvoicePage(invoice);
            invoicePage.SetTabManager(this);
            invoicePage.SetDraftId(draftId);

            AddTab(title, invoicePage);
        }

        // ===========================
        // BUTTONS
        // ===========================
        private async void NewInvoice_Click(object sender, RoutedEventArgs e)
            => await AddNewInvoiceTabAsync();

        private void BackToDashboard_Click(object sender, RoutedEventArgs e)
            => _layout.Navigate(new DashboardPage(_layout));
    }
}