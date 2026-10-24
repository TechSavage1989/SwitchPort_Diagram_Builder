
Application icon and report logos (1.10.10)
------------------------------------------
The supplied updated HUBTech Diagram Builder.svg is preserved unchanged as
the editable master, including Illustrator editing data. The flat artwork
uses navy, cyan, mint, amber, slate, magenta and off-white colors. It depicts
two port panels, colored VLAN dots in ports and keys, and a matching magenta
trunk rectangle. Branding/ also contains its
1024px PNG, multi-resolution ICO and color palette. The Windows icon is
embedded through ApplicationIcon during compilation, with the same image
used for the window/taskbar and the app header. No compiled EXE is modified
after the build. Edit the SVG in Inkscape, Illustrator or another SVG editor;
regenerate PNG/ICO exports when changing the artwork, then rebuild the app.

Select Report logo (optional) to choose a PNG, JPG or WebP image. The app
shows a preview and remembers a normalized copy for later sessions. Remove
clears it. Logo changes require a new report build. Logos appear in the
header of both PNG charts and every PDF booklet page, on a small white
backplate. Proportions and transparency are preserved. Port, VLAN and A4
geometry do not change. Text exports and inventory data remain text-only.
The original image is not modified. Input limit: 10 MB and 32 megapixels;
logos are normalized to a maximum dimension of 1024px for consistent output.
CLI: --logo path/to/logo.png. PNG/JPG/WebP are accepted; SVG source artwork
can be exported to PNG first. Native Windows taskbar and dialog behavior
still needs Windows testing; build/resource and browser checks ran on Linux.
