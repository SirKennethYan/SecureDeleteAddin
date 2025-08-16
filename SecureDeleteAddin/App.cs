using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace SecureDeleteAddin
{
    public class App : IExternalApplication
    {
        // Config
        private const string RequiredCode = "3001";
        private const int ThresholdCount = 10;
        private static readonly TimeSpan Grace = TimeSpan.FromMinutes(15);
        private static DateTime _codeValidUntil = DateTime.MinValue;

        private static readonly HashSet<string> HighRiskCats =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Levels", "Grids", "Topography", "Scope Boxes", "Project Information", "Floors" };

        public Result OnStartup(UIControlledApplication app)
        {
            var delId = RevitCommandId.LookupPostableCommandId(PostableCommand.Delete);
            var binding = app.CreateAddInCommandBinding(delId);

            binding.CanExecute += (s, e) => { e.CanExecute = e.ActiveDocument != null; };
            binding.BeforeExecuted += OnBeforeDelete;
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;

        private void OnBeforeDelete(object sender, BeforeExecutedEventArgs e)
        {
            var doc = e.ActiveDocument;
            if (doc == null) { e.Cancel = true; return; }
            var uidoc = new UIDocument(doc);

            var selIds = uidoc.Selection.GetElementIds()?.ToList() ?? new List<ElementId>();
            if (selIds.Count == 0)
            {
                TaskDialog.Show("Secure Delete", "Nothing selected.");
                e.Cancel = true;
                return;
            }

            // Build review list and detect high-risk
            var items = new List<ElemItem>();
            bool hasHighRisk = false;
            foreach (var id in selIds)
            {
                var el = doc.GetElement(id);
                if (el == null) continue;
                string cat = el.Category?.Name ?? "<No Category>";
                // name fallback
                string name = string.IsNullOrWhiteSpace(el.Name) ? $"Id {el.Id.Value}" : el.Name;
                items.Add(new ElemItem { Id = id, Label = $"{cat} — {name}", Category = cat });
                if (HighRiskCats.Contains(cat)) hasHighRisk = true;
            }

            // Decide if code is required
            bool requireCode = hasHighRisk || selIds.Count >= ThresholdCount;
            if (DateTime.UtcNow <= _codeValidUntil) requireCode = false; // grace window active

            // Show dialog (checklist always; password only if required)
            using (var dlg = new SecureDeleteDialog(items, requireCode))
            {
                if (dlg.ShowDialog() != WinForms.DialogResult.OK) { e.Cancel = true; return; }

                if (requireCode)
                {
                    if (!string.Equals(dlg.Code?.Trim(), RequiredCode, StringComparison.Ordinal))
                    {
                        TaskDialog.Show("Secure Delete", "Incorrect code. Deletion blocked.");
                        e.Cancel = true;
                        return;
                    }
                    _codeValidUntil = DateTime.UtcNow + Grace; // start/refresh grace
                }

                var idsToDelete = dlg.CheckedIds.ToList();
                if (idsToDelete.Count == 0)
                {
                    TaskDialog.Show("Secure Delete", "No elements selected for deletion.");
                    e.Cancel = true;
                    return;
                }

                uidoc.Selection.SetElementIds(new HashSet<ElementId>(idsToDelete));
                // allow native Delete to proceed
            }
        }

        private class ElemItem
        {
            public ElementId Id { get; set; }
            public string Label { get; set; }
            public string Category { get; set; }
            public override string ToString() => Label;
        }

        private class SecureDeleteDialog : WinForms.Form
        {
            private readonly WinForms.CheckedListBox _list;
            private readonly WinForms.TextBox _codeBox;
            public string Code => _codeBox?.Text;
            public IEnumerable<ElementId> CheckedIds => _list.CheckedItems.Cast<ElemItem>().Select(i => i.Id);

            public SecureDeleteDialog(IEnumerable<ElemItem> items, bool requireCode)
            {
                Text = "Secure Delete";
                StartPosition = WinForms.FormStartPosition.CenterParent;
                FormBorderStyle = WinForms.FormBorderStyle.FixedDialog;
                MaximizeBox = false; MinimizeBox = false;

                int width = 520, height = requireCode ? 440 : 400;
                ClientSize = new Drawing.Size(width, height);

                var lbl = new WinForms.Label
                {
                    Left = 12,
                    Top = 12,
                    Width = width - 24,
                    Text = "Review elements to delete. Uncheck any you don't want to delete."
                };

                _list = new WinForms.CheckedListBox
                {
                    Left = 12,
                    Top = 36,
                    Width = width - 24,
                    Height = requireCode ? 320 : 300,
                    CheckOnClick = true
                };
                foreach (var it in items) _list.Items.Add(it, true);

                Controls.Add(lbl);
                Controls.Add(_list);

                int buttonsTop = requireCode ? 380 : 340;

                if (requireCode)
                {
                    var codeLbl = new WinForms.Label { Left = 12, Top = 364, Width = 50, Text = "Code:" };
                    _codeBox = new WinForms.TextBox { Left = 62, Top = 360, Width = 180, UseSystemPasswordChar = true };
                    Controls.Add(codeLbl);
                    Controls.Add(_codeBox);
                }

                var ok = new WinForms.Button { Left = width - 180, Top = buttonsTop, Width = 80, Text = "OK", DialogResult = WinForms.DialogResult.OK };
                var cancel = new WinForms.Button { Left = width - 92, Top = buttonsTop, Width = 80, Text = "Cancel", DialogResult = WinForms.DialogResult.Cancel };
                Controls.Add(ok);
                Controls.Add(cancel);
                AcceptButton = ok; CancelButton = cancel;
            }
        }
    }
}
