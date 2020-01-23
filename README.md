# CANBUS-Analyzer
A development tool / companion software for Scan My Tesla. Graphs, displays and analyzes both known and unknown CANBUS packets.

CANBUS-Analyzer was inspired by comma.ai Cabana, but I wanted the same possibilities with my own data and formulas. 
To learn how to reverse engineer CANBUS data, see comma.ai's tutorials!

![Main window screenshot](screenshot.png)


Ctrl-click or shift-click to select/unselect messages and signals.


Plotting is done by OxyPlot library, which has some great, but very hard to find keyboard and mouse shortcuts:

Short summary: Scroll = zoom, scroll on an axis = zoom 1 axis. Click the plot window + press HOME to reset all axis.

Long version from here: https://stackoverflow.com/questions/27144051/which-keyboard-shortcut-functions-are-already-implemented-in-oxyplot

Pan*: Right mouse button, Alt+Left mouse button, Up/Down/Left/Right arrow key, Ctrl+Arrow key for fine pan

Pan-Zoom: Touch (don't know the details on that)

Zoom*: Mouse wheel, Ctrl+Mouse wheel for fine zoom

Zoom in*: Mouse extra button 1, 'Add', 'PageUp', Ctrl+'Add'/'PageUp' for fine

Zoom out*: Mouse extra button 2, 'Subtract', 'PageDown', Ctrl+'Subtract'/'PageDown' for fine

Zoom by rectangle: Ctrl+Right mouse button, Middle mouse button, Ctrl+Alt+Left mouse button

Reset*: Ctrl+Right mouse button double-click, Middle mouse button double-click, Ctrl+Alt+Left mouse button double-click

Reset axes: A, Home, Shake-Gesture (I guess on a mobile device)

Copy bitmap: Ctrl+C

Copy code: Ctrl+Alt+C

Copy properties: Ctrl+Alt+R

Tracker: Left mouse button, Shift+Left mouse button for points only tracker, Ctrl+Left mouse button for free tracker (show mouse coordinates basically)
