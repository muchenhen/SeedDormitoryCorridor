using System.ComponentModel;
using System.Runtime.InteropServices;
using SeedDormitoryCorridor.Rendering;

namespace SeedDormitoryCorridor.Platform.Windows;

public sealed class LayeredPetWindow : Form
{
    private const int DragThreshold = 4;
    private readonly ILayeredWindowRenderer renderer;
    private readonly LayeredSurfacePresenter presenter = new();
    private bool dragging;
    private Point dragOrigin;
    private Point windowOrigin;
    private bool fullClickThrough;

    public LayeredPetWindow(ILayeredWindowRenderer renderer)
    {
        this.renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Text = "白荆科技宿舍走廊";
        ClientSize = renderer.PixelSize;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true);
    }

    public event EventHandler? PetSingleClick;
    public event EventHandler? PetDoubleClick;
    public event EventHandler<PetDragDirectionEventArgs>? PetDragDirectionChanged;
    public event EventHandler? PetDragEnded;
    public event EventHandler? DpiScaleChanged;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public byte AlphaThreshold { get; set; } = 16;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool FullClickThrough
    {
        get => fullClickThrough;
        set
        {
            if (fullClickThrough == value)
            {
                return;
            }

            fullClickThrough = value;
            if (IsHandleCreated)
            {
                nint style = NativeMethods.GetWindowLongPtr(Handle, NativeMethods.GwlExStyle);
                long updated = value ? style.ToInt64() | NativeMethods.WsExTransparent : style.ToInt64() & ~NativeMethods.WsExTransparent;
                NativeMethods.SetWindowLongPtr(Handle, NativeMethods.GwlExStyle, new nint(updated));
            }
        }
    }

    public void ApplyFrame(in SpriteRenderFrame frame)
    {
        renderer.Render(frame);
        if (ClientSize != renderer.PixelSize)
        {
            ClientSize = renderer.PixelSize;
        }

        if (IsHandleCreated && Visible)
        {
            presenter.Present(Handle, renderer.Surface, Location);
        }
    }

    public void RefreshSurface()
    {
        if (IsHandleCreated && Visible)
        {
            presenter.Present(Handle, renderer.Surface, Location);
        }
    }

    public void SetTopMost(bool value)
    {
        TopMost = value;
        if (IsHandleCreated && !NativeMethods.SetWindowPos(Handle,
                value ? NativeMethods.HwndTopMost : NativeMethods.HwndNoTopMost, 0, 0, 0, 0,
                NativeMethods.SwpNoActivate | NativeMethods.SwpNoMove | NativeMethods.SwpNoSize))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SetWindowPos failed.");
        }
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |= NativeMethods.WsExLayered | NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate;
            if (TopMost)
            {
                parameters.ExStyle |= NativeMethods.WsExTopMost;
            }

            if (fullClickThrough)
            {
                parameters.ExStyle |= NativeMethods.WsExTransparent;
            }

            return parameters;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        RefreshSurface();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && !FullClickThrough)
        {
            dragging = false;
            dragOrigin = Cursor.Position;
            windowOrigin = Location;
            Capture = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!Capture || e.Button != MouseButtons.Left)
        {
            return;
        }

        Point current = Cursor.Position;
        int deltaX = current.X - dragOrigin.X;
        int deltaY = current.Y - dragOrigin.Y;
        if (!dragging && Math.Abs(deltaX) + Math.Abs(deltaY) < DragThreshold)
        {
            return;
        }

        dragging = true;
        Location = new Point(windowOrigin.X + deltaX, windowOrigin.Y + deltaY);
        RefreshSurface();
        if (Math.Abs(deltaX) >= DragThreshold)
        {
            PetDragDirectionChanged?.Invoke(this, new PetDragDirectionEventArgs(deltaX < 0));
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || !Capture)
        {
            return;
        }

        Capture = false;
        if (dragging)
        {
            PetDragEnded?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            PetSingleClick?.Invoke(this, EventArgs.Empty);
        }

        dragging = false;
    }

    protected override void OnDoubleClick(EventArgs e)
    {
        base.OnDoubleClick(e);
        PetDoubleClick?.Invoke(this, EventArgs.Empty);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WmMouseActivate)
        {
            m.Result = NativeMethods.MaNoActivate;
            return;
        }

        if (m.Msg == NativeMethods.WmNcHitTest)
        {
            int screenX = unchecked((short)(long)m.LParam);
            int screenY = unchecked((short)((long)m.LParam >> 16));
            Point client = PointToClient(new Point(screenX, screenY));
            m.Result = FullClickThrough || !renderer.HitTest(client.X, client.Y, AlphaThreshold)
                ? NativeMethods.HtTransparent
                : NativeMethods.HtClient;
            return;
        }

        if (m.Msg == NativeMethods.WmDpiChanged)
        {
            base.WndProc(ref m);
            DpiScaleChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            presenter.Dispose();
            renderer.Dispose();
        }

        base.Dispose(disposing);
    }
}

public sealed class PetDragDirectionEventArgs(bool left) : EventArgs
{
    public bool Left { get; } = left;
}
