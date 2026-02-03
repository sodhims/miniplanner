using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using dfd2wasm.Models;

namespace dfd2wasm.Pages;

public partial class DFDEditor
{
    // Icon library - SVG paths (Material Design Icons style)
    private static readonly Dictionary<string, (string Path, string ViewBox)> IconLibrary = new()
    {
        // People & Users
        ["user"] = ("M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z", "0 0 24 24"),
        ["users"] = ("M16 11c1.66 0 2.99-1.34 2.99-3S17.66 5 16 5c-1.66 0-3 1.34-3 3s1.34 3 3 3zm-8 0c1.66 0 2.99-1.34 2.99-3S9.66 5 8 5C6.34 5 5 6.34 5 8s1.34 3 3 3zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5c0-2.33-4.67-3.5-7-3.5zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.97 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5z", "0 0 24 24"),
        
        // Data & Storage
        ["database"] = ("M12 3C7.58 3 4 4.79 4 7v10c0 2.21 3.58 4 8 4s8-1.79 8-4V7c0-2.21-3.58-4-8-4zm0 2c3.87 0 6 1.5 6 2s-2.13 2-6 2-6-1.5-6-2 2.13-2 6-2zm6 12c0 .5-2.13 2-6 2s-6-1.5-6-2v-2.23c1.61.78 3.72 1.23 6 1.23s4.39-.45 6-1.23V17zm0-5c0 .5-2.13 2-6 2s-6-1.5-6-2V9.77c1.61.78 3.72 1.23 6 1.23s4.39-.45 6-1.23V12z", "0 0 24 24"),
        ["storage"] = ("M2 20h20v-4H2v4zm2-3h2v2H4v-2zM2 4v4h20V4H2zm4 3H4V5h2v2zm-4 7h20v-4H2v4zm2-3h2v2H4v-2z", "0 0 24 24"),
        ["folder"] = ("M10 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2h-8l-2-2z", "0 0 24 24"),
        ["file"] = ("M14 2H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z", "0 0 24 24"),
        
        // Cloud & Network
        ["cloud"] = ("M19.35 10.04C18.67 6.59 15.64 4 12 4 9.11 4 6.6 5.64 5.35 8.04 2.34 8.36 0 10.91 0 14c0 3.31 2.69 6 6 6h13c2.76 0 5-2.24 5-5 0-2.64-2.05-4.78-4.65-4.96z", "0 0 24 24"),
        ["cloud-upload"] = ("M19.35 10.04C18.67 6.59 15.64 4 12 4 9.11 4 6.6 5.64 5.35 8.04 2.34 8.36 0 10.91 0 14c0 3.31 2.69 6 6 6h13c2.76 0 5-2.24 5-5 0-2.64-2.05-4.78-4.65-4.96zM14 13v4h-4v-4H7l5-5 5 5h-3z", "0 0 24 24"),
        ["cloud-download"] = ("M19.35 10.04C18.67 6.59 15.64 4 12 4 9.11 4 6.6 5.64 5.35 8.04 2.34 8.36 0 10.91 0 14c0 3.31 2.69 6 6 6h13c2.76 0 5-2.24 5-5 0-2.64-2.05-4.78-4.65-4.96zM17 13l-5 5-5-5h3V9h4v4h3z", "0 0 24 24"),
        ["server"] = ("M20 13H4c-.55 0-1 .45-1 1v6c0 .55.45 1 1 1h16c.55 0 1-.45 1-1v-6c0-.55-.45-1-1-1zM7 19c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2zM20 3H4c-.55 0-1 .45-1 1v6c0 .55.45 1 1 1h16c.55 0 1-.45 1-1V4c0-.55-.45-1-1-1zM7 9c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2z", "0 0 24 24"),
        ["wifi"] = ("M1 9l2 2c4.97-4.97 13.03-4.97 18 0l2-2C16.93 2.93 7.08 2.93 1 9zm8 8l3 3 3-3c-1.65-1.66-4.34-1.66-6 0zm-4-4l2 2c2.76-2.76 7.24-2.76 10 0l2-2C15.14 9.14 8.87 9.14 5 13z", "0 0 24 24"),
        
        // Process & Actions
        ["gear"] = ("M19.14 12.94c.04-.31.06-.63.06-.94 0-.31-.02-.63-.06-.94l2.03-1.58c.18-.14.23-.41.12-.61l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54c-.04-.24-.24-.41-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.04.31-.06.63-.06.94s.02.63.06.94l-2.03 1.58c-.18.14-.23.41-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z", "0 0 24 24"),
        ["play"] = ("M8 5v14l11-7z", "0 0 24 24"),
        ["stop"] = ("M6 6h12v12H6z", "0 0 24 24"),
        ["refresh"] = ("M17.65 6.35C16.2 4.9 14.21 4 12 4c-4.42 0-7.99 3.58-7.99 8s3.57 8 7.99 8c3.73 0 6.84-2.55 7.73-6h-2.08c-.82 2.33-3.04 4-5.65 4-3.31 0-6-2.69-6-6s2.69-6 6-6c1.66 0 3.14.69 4.22 1.78L13 11h7V4l-2.35 2.35z", "0 0 24 24"),
        
        // Communication
        ["email"] = ("M20 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 4l-8 5-8-5V6l8 5 8-5v2z", "0 0 24 24"),
        ["chat"] = ("M21 6h-2v9H6v2c0 .55.45 1 1 1h11l4 4V7c0-.55-.45-1-1-1zm-4 6V3c0-.55-.45-1-1-1H3c-.55 0-1 .45-1 1v14l4-4h10c.55 0 1-.45 1-1z", "0 0 24 24"),
        ["phone"] = ("M6.62 10.79c1.44 2.83 3.76 5.14 6.59 6.59l2.2-2.2c.27-.27.67-.36 1.02-.24 1.12.37 2.33.57 3.57.57.55 0 1 .45 1 1V20c0 .55-.45 1-1 1-9.39 0-17-7.61-17-17 0-.55.45-1 1-1h3.5c.55 0 1 .45 1 1 0 1.25.2 2.45.57 3.57.11.35.03.74-.25 1.02l-2.2 2.2z", "0 0 24 24"),
        
        // Devices
        ["computer"] = ("M20 18c1.1 0 1.99-.9 1.99-2L22 6c0-1.1-.9-2-2-2H4c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2H0v2h24v-2h-4zM4 6h16v10H4V6z", "0 0 24 24"),
        ["mobile"] = ("M15.5 1h-8C6.12 1 5 2.12 5 3.5v17C5 21.88 6.12 23 7.5 23h8c1.38 0 2.5-1.12 2.5-2.5v-17C18 2.12 16.88 1 15.5 1zm-4 21c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5zm4.5-4H7V4h9v14z", "0 0 24 24"),
        ["printer"] = ("M19 8H5c-1.66 0-3 1.34-3 3v6h4v4h12v-4h4v-6c0-1.66-1.34-3-3-3zm-3 11H8v-5h8v5zm3-7c-.55 0-1-.45-1-1s.45-1 1-1 1 .45 1 1-.45 1-1 1zm-1-9H6v4h12V3z", "0 0 24 24"),
        
        // Security
        ["lock"] = ("M18 8h-1V6c0-2.76-2.24-5-5-5S7 3.24 7 6v2H6c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V10c0-1.1-.9-2-2-2zm-6 9c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2zm3.1-9H8.9V6c0-1.71 1.39-3.1 3.1-3.1 1.71 0 3.1 1.39 3.1 3.1v2z", "0 0 24 24"),
        ["shield"] = ("M12 1L3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4zm0 10.99h7c-.53 4.12-3.28 7.79-7 8.94V12H5V6.3l7-3.11v8.8z", "0 0 24 24"),
        ["key"] = ("M12.65 10C11.83 7.67 9.61 6 7 6c-3.31 0-6 2.69-6 6s2.69 6 6 6c2.61 0 4.83-1.67 5.65-4H17v4h4v-4h2v-4H12.65zM7 14c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2z", "0 0 24 24"),
        
        // Status & Alerts
        ["check"] = ("M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z", "0 0 24 24"),
        ["close"] = ("M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z", "0 0 24 24"),
        ["warning"] = ("M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z", "0 0 24 24"),
        ["info"] = ("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z", "0 0 24 24"),
        ["error"] = ("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z", "0 0 24 24"),
        ["star"] = ("M12 17.27L18.18 21l-1.64-7.03L22 9.24l-7.19-.61L12 2 9.19 8.63 2 9.24l5.46 4.73L5.82 21z", "0 0 24 24"),
        
        // Arrows & Navigation  
        ["arrow-right"] = ("M12 4l-1.41 1.41L16.17 11H4v2h12.17l-5.58 5.59L12 20l8-8z", "0 0 24 24"),
        ["arrow-left"] = ("M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z", "0 0 24 24"),
        ["arrow-up"] = ("M4 12l1.41 1.41L11 7.83V20h2V7.83l5.58 5.59L20 12l-8-8z", "0 0 24 24"),
        ["arrow-down"] = ("M20 12l-1.41-1.41L13 16.17V4h-2v12.17l-5.58-5.59L4 12l8 8z", "0 0 24 24"),
        
        // Business
        ["cart"] = ("M7 18c-1.1 0-1.99.9-1.99 2S5.9 22 7 22s2-.9 2-2-.9-2-2-2zM1 2v2h2l3.6 7.59-1.35 2.45c-.16.28-.25.61-.25.96 0 1.1.9 2 2 2h12v-2H7.42c-.14 0-.25-.11-.25-.25l.03-.12.9-1.63h7.45c.75 0 1.41-.41 1.75-1.03l3.58-6.49c.08-.14.12-.31.12-.48 0-.55-.45-1-1-1H5.21l-.94-2H1zm16 16c-1.1 0-1.99.9-1.99 2s.89 2 1.99 2 2-.9 2-2-.9-2-2-2z", "0 0 24 24"),
        ["credit-card"] = ("M20 4H4c-1.11 0-1.99.89-1.99 2L2 18c0 1.11.89 2 2 2h16c1.11 0 2-.89 2-2V6c0-1.11-.89-2-2-2zm0 14H4v-6h16v6zm0-10H4V6h16v2z", "0 0 24 24"),
        ["building"] = ("M12 7V3H2v18h20V7H12zM6 19H4v-2h2v2zm0-4H4v-2h2v2zm0-4H4V9h2v2zm0-4H4V5h2v2zm4 12H8v-2h2v2zm0-4H8v-2h2v2zm0-4H8V9h2v2zm0-4H8V5h2v2zm10 12h-8v-2h2v-2h-2v-2h2v-2h-2V9h8v10zm-2-8h-2v2h2v-2zm0 4h-2v2h2v-2z", "0 0 24 24"),
        
        // Development & Tech
        ["code"] = ("M9.4 16.6L4.8 12l4.6-4.6L8 6l-6 6 6 6 1.4-1.4zm5.2 0l4.6-4.6-4.6-4.6L16 6l6 6-6 6-1.4-1.4z", "0 0 24 24"),
        ["api"] = ("M14 12l-2 2-2-2 2-2 2 2zm-2-6l2.12 2.12 2.5-2.5L12 1 7.38 5.62l2.5 2.5L12 6zm-6 6l2.12-2.12-2.5-2.5L1 12l4.62 4.62 2.5-2.5L6 12zm12 0l-2.12 2.12 2.5 2.5L23 12l-4.62-4.62-2.5 2.5L18 12zm-6 6l-2.12-2.12-2.5 2.5L12 23l4.62-4.62-2.5-2.5L12 18z", "0 0 24 24"),
        ["terminal"] = ("M20 19.59V8l-6-6H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c.45 0 .85-.15 1.19-.4l-4.43-4.43c-.8.52-1.74.83-2.76.83-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5c0 1.02-.31 1.96-.83 2.75L20 19.59z", "0 0 24 24"),
        ["bug"] = ("M20 8h-2.81c-.45-.78-1.07-1.45-1.82-1.96L17 4.41 15.59 3l-2.17 2.17C12.96 5.06 12.49 5 12 5c-.49 0-.96.06-1.41.17L8.41 3 7 4.41l1.62 1.63C7.88 6.55 7.26 7.22 6.81 8H4v2h2.09c-.05.33-.09.66-.09 1v1H4v2h2v1c0 .34.04.67.09 1H4v2h2.81c1.04 1.79 2.97 3 5.19 3s4.15-1.21 5.19-3H20v-2h-2.09c.05-.33.09-.66.09-1v-1h2v-2h-2v-1c0-.34-.04-.67-.09-1H20V8zm-6 8h-4v-2h4v2zm0-4h-4v-2h4v2z", "0 0 24 24"),
        
        // Other
        ["lightbulb"] = ("M9 21c0 .55.45 1 1 1h4c.55 0 1-.45 1-1v-1H9v1zm3-19C8.14 2 5 5.14 5 9c0 2.38 1.19 4.47 3 5.74V17c0 .55.45 1 1 1h6c.55 0 1-.45 1-1v-2.26c1.81-1.27 3-3.36 3-5.74 0-3.86-3.14-7-7-7zm2.85 11.1l-.85.6V16h-4v-2.3l-.85-.6C7.8 12.16 7 10.63 7 9c0-2.76 2.24-5 5-5s5 2.24 5 5c0 1.63-.8 3.16-2.15 4.1z", "0 0 24 24"),
        ["link"] = ("M3.9 12c0-1.71 1.39-3.1 3.1-3.1h4V7H7c-2.76 0-5 2.24-5 5s2.24 5 5 5h4v-1.9H7c-1.71 0-3.1-1.39-3.1-3.1zM8 13h8v-2H8v2zm9-6h-4v1.9h4c1.71 0 3.1 1.39 3.1 3.1s-1.39 3.1-3.1 3.1h-4V17h4c2.76 0 5-2.24 5-5s-2.24-5-5-5z", "0 0 24 24"),
        ["clock"] = ("M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8zm.5-13H11v6l5.25 3.15.75-1.23-4.5-2.67z", "0 0 24 24"),
        ["location"] = ("M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z", "0 0 24 24"),
        ["search"] = ("M15.5 14h-.79l-.28-.27C15.41 12.59 16 11.11 16 9.5 16 5.91 13.09 3 9.5 3S3 5.91 3 9.5 5.91 16 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z", "0 0 24 24"),
        ["home"] = ("M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z", "0 0 24 24"),
        ["document"] = ("M14 2H6c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8l-6-6zM6 20V4h7v5h5v11H6z", "0 0 24 24"),
    };

    // Icon categories for organized UI display
    private static readonly Dictionary<string, string[]> IconCategories = new()
    {
        ["People"] = new[] { "user", "users" },
        ["Data"] = new[] { "database", "storage", "folder", "file", "document" },
        ["Cloud"] = new[] { "cloud", "cloud-upload", "cloud-download", "server", "wifi" },
        ["Process"] = new[] { "gear", "play", "stop", "refresh", "clock" },
        ["Communication"] = new[] { "email", "chat", "phone" },
        ["Devices"] = new[] { "computer", "mobile", "printer" },
        ["Security"] = new[] { "lock", "shield", "key" },
        ["Status"] = new[] { "check", "close", "warning", "info", "error", "star" },
        ["Arrows"] = new[] { "arrow-right", "arrow-left", "arrow-up", "arrow-down" },
        ["Business"] = new[] { "cart", "credit-card", "building" },
        ["Tech"] = new[] { "code", "api", "terminal", "bug" },
        ["Other"] = new[] { "lightbulb", "link", "search", "home", "location" },
    };

    // Updated RenderNodeText that includes icon
    // Applies counter-rotation to keep text and icons upright when node is rotated
    private RenderFragment RenderNodeTextWithIcon(Node node) => builder =>
    {
        int seq = 0;

        // Skip text rendering for circuit components - they have labels rendered by the shape itself
        if (node.TemplateId == "circuit" && !string.IsNullOrEmpty(node.ComponentLabel))
        {
            return;
        }

        // Check if node has SVG or Image attachments to render inside the node
        var imageAttachment = node.Attachments?.FirstOrDefault(a => a.FileType == AttachmentType.Svg || a.FileType == AttachmentType.Image);
        if (imageAttachment != null)
        {
            // Render image attachment inside the node, scaled to fit with padding
            var padding = 8.0;
            var imgWidth = node.Width - padding * 2;
            var imgHeight = node.Height - padding * 2;

            // If there's text, leave room for it at the bottom
            var hasText = !string.IsNullOrWhiteSpace(node.Text);
            if (hasText)
            {
                imgHeight -= 20; // Leave space for text label
            }

            builder.OpenElement(seq++, "image");
            builder.AddAttribute(seq++, "x", padding.ToString());
            builder.AddAttribute(seq++, "y", padding.ToString());
            builder.AddAttribute(seq++, "width", imgWidth.ToString());
            builder.AddAttribute(seq++, "height", imgHeight.ToString());
            builder.AddAttribute(seq++, "href", imageAttachment.DataUri);
            builder.AddAttribute(seq++, "preserveAspectRatio", "xMidYMid meet");
            builder.AddAttribute(seq++, "style", "pointer-events: none;");
            builder.CloseElement();

            // Render text label at bottom if present
            if (hasText)
            {
                var textY = node.Height - 10;
                builder.OpenElement(seq++, "text");
                builder.AddAttribute(seq++, "x", (node.Width / 2).ToString());
                builder.AddAttribute(seq++, "y", textY.ToString());
                builder.AddAttribute(seq++, "text-anchor", "middle");
                builder.AddAttribute(seq++, "dominant-baseline", "middle");
                builder.AddAttribute(seq++, "fill", "#374151");
                builder.AddAttribute(seq++, "font-size", "12");
                builder.AddAttribute(seq++, "style", "pointer-events: none; user-select: none;");
                builder.AddContent(seq++, node.Text);
                builder.CloseElement();
            }

            return; // Don't render icon/normal text when image is displayed
        }

        // Check if node has PDF attachments to render as clickable icon
        var pdfAttachment = node.Attachments?.FirstOrDefault(a => a.FileType == AttachmentType.Pdf);
        if (pdfAttachment != null)
        {
            var padding = 8.0;
            var iconSize = Math.Min(node.Width, node.Height) - padding * 2 - 20; // Leave room for text
            iconSize = Math.Max(iconSize, 40); // Minimum icon size
            var iconX = (node.Width - iconSize) / 2;
            var iconY = padding;

            var hasText = !string.IsNullOrWhiteSpace(node.Text);
            if (!hasText)
            {
                iconY = (node.Height - iconSize) / 2;
            }

            // PDF icon background (red document shape)
            builder.OpenElement(seq++, "g");
            builder.AddAttribute(seq++, "class", "pdf-icon");
            builder.AddAttribute(seq++, "data-pdf-uri", pdfAttachment.DataUri);
            builder.AddAttribute(seq++, "style", "cursor: pointer;");

            // Document body
            builder.OpenElement(seq++, "rect");
            builder.AddAttribute(seq++, "x", iconX.ToString());
            builder.AddAttribute(seq++, "y", iconY.ToString());
            builder.AddAttribute(seq++, "width", iconSize.ToString());
            builder.AddAttribute(seq++, "height", iconSize.ToString());
            builder.AddAttribute(seq++, "rx", "4");
            builder.AddAttribute(seq++, "fill", "#dc2626");
            builder.AddAttribute(seq++, "stroke", "#991b1b");
            builder.AddAttribute(seq++, "stroke-width", "1");
            builder.CloseElement();

            // Folded corner
            var cornerSize = iconSize * 0.25;
            builder.OpenElement(seq++, "path");
            builder.AddAttribute(seq++, "d", $"M {iconX + iconSize - cornerSize} {iconY} L {iconX + iconSize} {iconY + cornerSize} L {iconX + iconSize - cornerSize} {iconY + cornerSize} Z");
            builder.AddAttribute(seq++, "fill", "#fecaca");
            builder.CloseElement();

            // PDF text
            var fontSize = iconSize * 0.25;
            builder.OpenElement(seq++, "text");
            builder.AddAttribute(seq++, "x", (iconX + iconSize / 2).ToString());
            builder.AddAttribute(seq++, "y", (iconY + iconSize / 2 + fontSize * 0.35).ToString());
            builder.AddAttribute(seq++, "text-anchor", "middle");
            builder.AddAttribute(seq++, "fill", "white");
            builder.AddAttribute(seq++, "font-size", fontSize.ToString());
            builder.AddAttribute(seq++, "font-weight", "bold");
            builder.AddAttribute(seq++, "style", "pointer-events: none;");
            builder.AddContent(seq++, "PDF");
            builder.CloseElement();

            // Click hint at bottom of icon
            var hintFontSize = Math.Max(8, iconSize * 0.12);
            builder.OpenElement(seq++, "text");
            builder.AddAttribute(seq++, "x", (iconX + iconSize / 2).ToString());
            builder.AddAttribute(seq++, "y", (iconY + iconSize - hintFontSize * 0.5).ToString());
            builder.AddAttribute(seq++, "text-anchor", "middle");
            builder.AddAttribute(seq++, "fill", "white");
            builder.AddAttribute(seq++, "font-size", hintFontSize.ToString());
            builder.AddAttribute(seq++, "opacity", "0.8");
            builder.AddAttribute(seq++, "style", "pointer-events: none;");
            builder.AddContent(seq++, "Click to view");
            builder.CloseElement();

            builder.CloseElement(); // g

            // Render text label at bottom if present
            if (hasText)
            {
                var textY = node.Height - 10;
                builder.OpenElement(seq++, "text");
                builder.AddAttribute(seq++, "x", (node.Width / 2).ToString());
                builder.AddAttribute(seq++, "y", textY.ToString());
                builder.AddAttribute(seq++, "text-anchor", "middle");
                builder.AddAttribute(seq++, "dominant-baseline", "middle");
                builder.AddAttribute(seq++, "fill", "#374151");
                builder.AddAttribute(seq++, "font-size", "12");
                builder.AddAttribute(seq++, "style", "pointer-events: none; user-select: none;");
                builder.AddContent(seq++, node.Text);
                builder.CloseElement();
            }

            return; // Don't render icon/normal text when PDF is displayed
        }

        // Check if node has an icon
        var hasIcon = !string.IsNullOrEmpty(node.Icon) && IconLibrary.ContainsKey(node.Icon);

        // Calculate center for counter-rotation
        var nodeCenterX = node.Width / 2;
        var nodeCenterY = node.Height / 2;
        var counterRotation = node.Rotation != 0
            ? $"rotate({-node.Rotation}, {nodeCenterX.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {nodeCenterY.ToString(System.Globalization.CultureInfo.InvariantCulture)})"
            : null;

        // Render icon if present
        if (hasIcon)
        {
            var (path, viewBox) = IconLibrary[node.Icon!];
            var iconSize = 20.0;
            var iconX = (node.Width - iconSize) / 2;
            var iconY = string.IsNullOrWhiteSpace(node.Text) ? (node.Height - iconSize) / 2 : 8.0;

            // Wrap icon in a group with counter-rotation if needed
            if (counterRotation != null)
            {
                builder.OpenElement(seq++, "g");
                builder.AddAttribute(seq++, "transform", counterRotation);
            }

            builder.OpenElement(seq++, "svg");
            builder.AddAttribute(seq++, "x", iconX.ToString());
            builder.AddAttribute(seq++, "y", iconY.ToString());
            builder.AddAttribute(seq++, "width", iconSize.ToString());
            builder.AddAttribute(seq++, "height", iconSize.ToString());
            builder.AddAttribute(seq++, "viewBox", viewBox);
            builder.AddAttribute(seq++, "style", "pointer-events: none;");

            builder.OpenElement(seq++, "path");
            builder.AddAttribute(seq++, "d", path);
            builder.AddAttribute(seq++, "fill", node.StrokeColor);
            builder.CloseElement(); // path

            builder.CloseElement(); // svg

            if (counterRotation != null)
            {
                builder.CloseElement(); // g
            }
        }

        // Render text
        var textLines = node.Text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var lineHeight = 16.0;
        var centerX = node.Width / 2;
        var textOffsetY = hasIcon ? 12.0 : 0;

        if (textLines.Length <= 1)
        {
            var textY = hasIcon ? (node.Height / 2 + textOffsetY) : (node.Height / 2);

            builder.OpenElement(seq++, "text");
            builder.AddAttribute(seq++, "x", centerX.ToString());
            builder.AddAttribute(seq++, "y", textY.ToString());
            builder.AddAttribute(seq++, "text-anchor", "middle");
            builder.AddAttribute(seq++, "dominant-baseline", "middle");
            builder.AddAttribute(seq++, "fill", "#374151");
            builder.AddAttribute(seq++, "font-size", "14");
            if (counterRotation != null)
            {
                builder.AddAttribute(seq++, "transform", counterRotation);
            }
            builder.AddAttribute(seq++, "style", "pointer-events: none; user-select: none;");
            builder.AddContent(seq++, node.Text);
            builder.CloseElement();
        }
        else
        {
            var totalHeight = textLines.Length * lineHeight;
            var startY = hasIcon
                ? ((node.Height - totalHeight) / 2 + lineHeight / 2 + textOffsetY)
                : ((node.Height - totalHeight) / 2 + lineHeight / 2);

            for (int i = 0; i < textLines.Length; i++)
            {
                var lineY = startY + i * lineHeight;
                builder.OpenElement(seq++, "text");
                builder.AddAttribute(seq++, "x", centerX.ToString());
                builder.AddAttribute(seq++, "y", lineY.ToString());
                builder.AddAttribute(seq++, "text-anchor", "middle");
                builder.AddAttribute(seq++, "dominant-baseline", "middle");
                builder.AddAttribute(seq++, "fill", "#374151");
                builder.AddAttribute(seq++, "font-size", "14");
                if (counterRotation != null)
                {
                    builder.AddAttribute(seq++, "transform", counterRotation);
                }
                builder.AddAttribute(seq++, "style", "pointer-events: none; user-select: none;");
                builder.AddContent(seq++, textLines[i]);
                builder.CloseElement();
            }
        }

        // Render attachments indicator if node has attachments
        if (node.Attachments?.Any() == true)
        {
            var attachCount = node.Attachments.Count;
            var badgeX = node.Width - 18;
            var badgeY = 4;

            // Background circle
            builder.OpenElement(seq++, "circle");
            builder.AddAttribute(seq++, "cx", (badgeX + 7).ToString());
            builder.AddAttribute(seq++, "cy", (badgeY + 7).ToString());
            builder.AddAttribute(seq++, "r", "9");
            builder.AddAttribute(seq++, "fill", "#3b82f6");
            builder.AddAttribute(seq++, "stroke", "white");
            builder.AddAttribute(seq++, "stroke-width", "1");
            builder.CloseElement();

            // Attachment count text
            builder.OpenElement(seq++, "text");
            builder.AddAttribute(seq++, "x", (badgeX + 7).ToString());
            builder.AddAttribute(seq++, "y", (badgeY + 8).ToString());
            builder.AddAttribute(seq++, "text-anchor", "middle");
            builder.AddAttribute(seq++, "dominant-baseline", "middle");
            builder.AddAttribute(seq++, "fill", "white");
            builder.AddAttribute(seq++, "font-size", "10");
            builder.AddAttribute(seq++, "font-weight", "bold");
            builder.AddAttribute(seq++, "style", "pointer-events: none;");
            builder.AddContent(seq++, attachCount.ToString());
            builder.CloseElement();
        }
    };

    // Render attachments for a node (called when node is selected and expanded)
    private RenderFragment RenderNodeAttachments(Node node) => builder =>
    {
        if (node.Attachments == null || !node.Attachments.Any()) return;

        int seq = 0;
        var startY = node.Height + 5;
        var attachmentHeight = 30;
        var padding = 4;

        foreach (var att in node.Attachments)
        {
            if (att.FileType == AttachmentType.Svg)
            {
                // Render SVG image directly
                builder.OpenElement(seq++, "image");
                builder.AddAttribute(seq++, "x", padding.ToString());
                builder.AddAttribute(seq++, "y", startY.ToString());
                builder.AddAttribute(seq++, "width", (node.Width - padding * 2).ToString());
                builder.AddAttribute(seq++, "height", attachmentHeight.ToString());
                builder.AddAttribute(seq++, "href", att.DataUri);
                builder.AddAttribute(seq++, "preserveAspectRatio", "xMidYMid meet");
                builder.CloseElement();
            }
            else if (att.FileType == AttachmentType.Pdf)
            {
                // Render PDF placeholder with link
                builder.OpenElement(seq++, "rect");
                builder.AddAttribute(seq++, "x", padding.ToString());
                builder.AddAttribute(seq++, "y", startY.ToString());
                builder.AddAttribute(seq++, "width", (node.Width - padding * 2).ToString());
                builder.AddAttribute(seq++, "height", attachmentHeight.ToString());
                builder.AddAttribute(seq++, "fill", "#fef3c7");
                builder.AddAttribute(seq++, "stroke", "#f59e0b");
                builder.AddAttribute(seq++, "rx", "4");
                builder.CloseElement();

                // PDF icon text
                builder.OpenElement(seq++, "text");
                builder.AddAttribute(seq++, "x", (node.Width / 2).ToString());
                builder.AddAttribute(seq++, "y", (startY + attachmentHeight / 2).ToString());
                builder.AddAttribute(seq++, "text-anchor", "middle");
                builder.AddAttribute(seq++, "dominant-baseline", "middle");
                builder.AddAttribute(seq++, "fill", "#92400e");
                builder.AddAttribute(seq++, "font-size", "11");
                builder.AddContent(seq++, $"📄 {att.FileName}");
                builder.CloseElement();
            }

            startY += attachmentHeight + 4;
        }
    };
}
