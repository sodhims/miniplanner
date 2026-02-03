window.downloadFile = function (filename, content) {
    try {
        const blob = new Blob([content], { type: 'application/octet-stream' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = filename;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
        console.log('Download triggered for: ' + filename);
    } catch (e) {
        console.error('Download error:', e);
    }
};

// Open a PDF data URI in a new browser tab
window.openPdfViewer = function (dataUri) {
    try {
        // Open in a new tab - browser's built-in PDF viewer will handle it
        const newWindow = window.open();
        if (newWindow) {
            newWindow.document.write(`
                <!DOCTYPE html>
                <html style="height: 100%; margin: 0;">
                <head><title>PDF Viewer</title></head>
                <body style="height: 100%; margin: 0; display: flex; flex-direction: column;">
                    <embed src="${dataUri}" type="application/pdf" style="flex: 1; width: 100%;" />
                </body>
                </html>
            `);
            newWindow.document.close();
        } else {
            // Fallback: download the PDF if popup blocked
            const link = document.createElement('a');
            link.href = dataUri;
            link.download = 'document.pdf';
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
        }
    } catch (e) {
        console.error('PDF viewer error:', e);
    }
};

// Initialize click handler for PDF icons in SVG
window.initPdfClickHandler = function () {
    document.addEventListener('click', function(e) {
        // Find if we clicked on or inside a pdf-icon group
        let target = e.target;
        while (target && target !== document) {
            if (target.classList && target.classList.contains('pdf-icon')) {
                const dataUri = target.getAttribute('data-pdf-uri');
                if (dataUri) {
                    e.stopPropagation();
                    e.preventDefault();
                    window.openPdfViewer(dataUri);
                    return;
                }
            }
            target = target.parentElement;
        }
    }, true); // Use capture phase to get event before Blazor
};