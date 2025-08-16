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
        private const string RequiredCode = "3001";

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
            // Checks if a document is open. If not, cancels the operation.
            var doc = e.ActiveDocument;
            if (doc == null) { e.Cancel = true; return; }
            var uidoc = new UIDocument(doc);

            // Get selected elements. If nothing is selected, cancels the operation.
            var selIds = uidoc.Selection.GetElementIds()?.ToList() ?? new List<ElementId>();
            if (selIds.Count == 0)
            {
                TaskDialog.Show("Secure Delete", "Nothing selected.");
                e.Cancel = true;
                return;
            }

            // Builds a review list of selected elements, showing their category and name.
            var items = new List<ElemItem>();
            foreach (var id in selIds)
            {
                var el = doc.GetElement(id);
                if (el == null) continue;
                string cat = el.Category?.Name ?? "<No Category>";
                string name = string.IsNullOrWhiteSpace(el.Name) ? $"Id {el.Id.IntegerValue}" : el.Name;
                items.Add(new ElemItem { Id = id, Label = $"{cat} — {name}" });
            }

            // Shows custom dialog.
            using (var dlg = new SecureDeleteDialog(items))
            {
                if (dlg.ShowDialog() != WinForms.DialogResult.OK) { e.Cancel = true; return; }

                if (!string.Equals(dlg.Code?.Trim(), RequiredCode, StringComparison.Ordinal))
                {
                    TaskDialog.Show("Secure Delete", "Incorrect code. Deletion blocked.");
                    e.Cancel = true;
                    return;
                }

                var idsToDelete = dlg.CheckedIds.ToList();
                if (idsToDelete.Count == 0)
                {
                    TaskDialog.Show("Secure Delete", "No elements selected for deletion.");
                    e.Cancel = true;
                    return;
                }

                // Limit native Delete to approved items
                uidoc.Selection.SetElementIds(new HashSet<ElementId>(idsToDelete));
                // Allow Delete to proceed on this selection
            }
        }

        // Data class to hold an element’s ElementId and a display label.
        private class ElemItem
        {
            public ElementId Id { get; set; }
            public string Label { get; set; }
            public override string ToString() => Label;
        }

        // Password + checklist dialog
        private class SecureDeleteDialog : WinForms.Form
        {
            private readonly WinForms.CheckedListBox _list;
            private readonly WinForms.TextBox _codeBox;
            private readonly WinForms.Button _ok;
            private readonly WinForms.Button _cancel;

            public string Code => _codeBox.Text;
            public IEnumerable<ElementId> CheckedIds
                => _list.CheckedItems.Cast<ElemItem>().Select(i => i.Id);

            public SecureDeleteDialog(IEnumerable<ElemItem> items)
            {
                Text = "Secure Delete";
                StartPosition = WinForms.FormStartPosition.CenterParent;
                FormBorderStyle = WinForms.FormBorderStyle.FixedDialog;
                MaximizeBox = false; MinimizeBox = false;
                ClientSize = new Drawing.Size(520, 440);

                var lbl = new WinForms.Label
                {
                    Left = 12,
                    Top = 12,
                    Width = 496,
                    Text = "Review elements to delete. Uncheck any you don't want to delete."
                };

                _list = new WinForms.CheckedListBox
                {
                    Left = 12,
                    Top = 36,
                    Width = 496,
                    Height = 340,
                    CheckOnClick = true
                };
                foreach (var it in items) _list.Items.Add(it, true);

                var codeLbl = new WinForms.Label { Left = 12, Top = 386, Width = 50, Text = "Code:" };
                _codeBox = new WinForms.TextBox { Left = 62, Top = 382, Width = 180, UseSystemPasswordChar = true };

                _ok = new WinForms.Button { Left = 340, Top = 380, Width = 80, Text = "OK", DialogResult = WinForms.DialogResult.OK };
                _cancel = new WinForms.Button { Left = 428, Top = 380, Width = 80, Text = "Cancel", DialogResult = WinForms.DialogResult.Cancel };

                Controls.AddRange(new WinForms.Control[] { lbl, _list, codeLbl, _codeBox, _ok, _cancel });
                AcceptButton = _ok; CancelButton = _cancel;
            }
        }
    }
}
