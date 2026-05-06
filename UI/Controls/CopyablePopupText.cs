namespace MT5TradingBot.UI
{
    internal static class CopyablePopupText
    {
        public static void Enable(Control root)
        {
            AttachRecursive(root);
            root.ControlAdded += (_, e) =>
            {
                if (e.Control != null)
                    AttachRecursive(e.Control);
            };
        }

        private static void AttachRecursive(Control control)
        {
            Attach(control);
            foreach (Control child in control.Controls)
                AttachRecursive(child);

            control.ControlAdded += (_, e) =>
            {
                if (e.Control != null)
                    AttachRecursive(e.Control);
            };
        }

        private static void Attach(Control control)
        {
            if (control.ContextMenuStrip != null)
                return;

            if (control is TextBoxBase textBox)
            {
                textBox.ReadOnly = textBox.ReadOnly;
                return;
            }

            if (control is Label or Panel or Form or GroupBox or FlowLayoutPanel or TableLayoutPanel)
                control.ContextMenuStrip = BuildMenu(control);
        }

        private static ContextMenuStrip BuildMenu(Control control)
        {
            var menu = new ContextMenuStrip();
            var copyThis = new ToolStripMenuItem("Copy text");
            copyThis.Click += (_, _) => CopyText(TextFor(control));
            var copyAll = new ToolStripMenuItem("Copy all visible text");
            copyAll.Click += (_, _) => CopyText(CollectVisibleText(control.FindForm() ?? control));
            menu.Opening += (_, e) =>
            {
                copyThis.Enabled = !string.IsNullOrWhiteSpace(TextFor(control));
                copyAll.Enabled = !string.IsNullOrWhiteSpace(CollectVisibleText(control.FindForm() ?? control));
                e.Cancel = !copyThis.Enabled && !copyAll.Enabled;
            };
            menu.Items.Add(copyThis);
            menu.Items.Add(copyAll);
            return menu;
        }

        private static string CollectVisibleText(Control root)
        {
            var lines = new List<string>();
            Collect(root, lines);
            return string.Join(Environment.NewLine, lines.Where(static line => !string.IsNullOrWhiteSpace(line)));
        }

        private static void Collect(Control control, List<string> lines)
        {
            if (!control.Visible)
                return;

            string text = TextFor(control);
            if (!string.IsNullOrWhiteSpace(text))
                lines.Add(text);

            foreach (Control child in control.Controls)
                Collect(child, lines);
        }

        private static string TextFor(Control control) =>
            control switch
            {
                TextBoxBase textBox => textBox.Text.Trim(),
                Label label => label.Text.Trim(),
                Button button => button.Text.Trim(),
                Form form => form.Text.Trim(),
                _ => ""
            };

        private static void CopyText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                Clipboard.SetText(text);
            }
            catch
            {
                // Copy support should never interrupt the trading UI.
            }
        }
    }
}
