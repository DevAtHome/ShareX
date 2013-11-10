﻿#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (C) 2008-2013 ShareX Developers

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace HelpersLib
{
    public class MyColorEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.Modal;
        }

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            if (value.GetType() != typeof(RGBA))
            {
                return value;
            }

            IWindowsFormsEditorService svc = (IWindowsFormsEditorService)provider.GetService(typeof(IWindowsFormsEditorService));

            if (svc != null)
            {
                RGBA color = (RGBA)value;

                using (DialogColor form = new DialogColor(color))
                {
                    if (svc.ShowDialog(form) == DialogResult.OK)
                    {
                        return form.NewColor.RGBA;
                    }
                }
            }

            return value;
        }

        public override bool GetPaintValueSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        public override void PaintValue(PaintValueEventArgs e)
        {
            Graphics g = e.Graphics;
            RGBA color = (RGBA)e.Value;

            if (color.Alpha < 255)
            {
                using (Image checker = ImageHelpers.CreateCheckers(e.Bounds.Width / 2, e.Bounds.Height / 2, Color.LightGray, Color.White))
                {
                    g.DrawImage(checker, e.Bounds);
                }
            }

            using (SolidBrush brush = new SolidBrush(color))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            e.Graphics.DrawRectangleProper(Pens.Black, e.Bounds);
        }
    }
}