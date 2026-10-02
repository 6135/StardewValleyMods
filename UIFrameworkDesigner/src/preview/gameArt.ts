// Game-art skin (architecture.md §7.3, phase 6): the vanilla textures read from the user's unpacked Content folder
// (File System Access API), cut into the pieces the framework draws and handed to PreviewPane.css as CSS images.
// Everything stays in memory (data: / blob: URLs of this page); nothing is uploaded or persisted.
//
// Source rects, from StardewUIFramework:
//   Rendering/Theme.cs   panel box Game1.menuTexture (0,256,60,60) at scale 1, button Game1.mouseCursors (432,439,9,9)
//                        at 4 (Draw.StyledBox), dropdown box (433,451,3,3) / button (437,450,10,11), checkbox
//                        (227,425,9,9) / checked (236,425,9,9), slider track (403,383,6,6) / knob (420,441,10,6), all at 4
//   Core/UIMenu.cs       the window is Game1.drawDialogueBox: Game1.menuTexture 64 px tiles, corners (0,0) (192,0)
//                        (0,192) (192,192), edges top (128,0) bottom (128,192) left (0,128) right (192,128), and the
//                        background (64,128) stretched 28 px inside the frame; the title is SpriteText's scroll,
//                        mouseCursors (325,318,12,18) + (337,318,1,18) stretched + (338,318,12,18) at 4
//   TextBoxDrawing.cs    LooseSprites/textBox: 16 px end caps and the 4 px column at x 16 stretched between them
//   ItemImage            Maps/springobjects: 16×16 sprites, row-major by object index
// IClickableMenu.drawTextureBox cuts its source into thirds and draws the corners at `scale`, so a 9-slice box is a CSS
// border-image with slice = width / 3 and border width = slice × scale.

/** CSS custom properties (url() values) for PreviewPane.css, and which sheets loaded (for the pv-art-* classes). */
export interface GameArt {
  vars: Record<string, string>;
  sheets: Array<'cursors' | 'menu' | 'textbox'>;
  /** Maps/springobjects as a blob URL, with its grid size in sprites. */
  objects?: { url: string; columns: number; rows: number };
}

type Rect = readonly [number, number, number, number];

interface DirectoryPickerWindow {
  showDirectoryPicker(options?: { id?: string; mode?: 'read' }): Promise<FileSystemDirectoryHandle>;
}

async function openFile(root: FileSystemDirectoryHandle, path: string): Promise<File | undefined> {
  const parts = path.split('/');
  try {
    let dir = root;
    for (const part of parts.slice(0, -1)) {
      dir = await dir.getDirectoryHandle(part);
    }
    return await (await dir.getFileHandle(parts[parts.length - 1]!)).getFile();
  } catch {
    return undefined;
  }
}

async function bitmap(root: FileSystemDirectoryHandle, path: string): Promise<ImageBitmap | undefined> {
  const file = await openFile(root, path);
  return file ? createImageBitmap(file) : undefined;
}

/** Draws `pieces` ([source rect, x, y, width, height] in the output) of `image` into a w×h PNG as a CSS url(). */
function compose(image: ImageBitmap, width: number, height: number, pieces: Array<[Rect, number, number, number, number]>): string {
  const canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;
  const g = canvas.getContext('2d')!;
  g.imageSmoothingEnabled = false;
  for (const [[sx, sy, sw, sh], x, y, w, h] of pieces) {
    g.drawImage(image, sx, sy, sw, sh, x, y, w, h);
  }
  return `url(${canvas.toDataURL('image/png')})`;
}

const crop = (image: ImageBitmap, r: Rect): string => compose(image, r[2], r[3], [[r, 0, 0, r[2], r[3]]]);

/**
 * Asks for the unpacked Content folder (or the game folder holding it) and reads the sheets; undefined when the user
 * cancels. Sheets that are missing (not unpacked) are left out, and the schematic skin draws those parts.
 */
export async function pickGameArt(): Promise<GameArt | undefined> {
  let root: FileSystemDirectoryHandle;
  try {
    root = await (window as unknown as DirectoryPickerWindow).showDirectoryPicker({ id: 'stardew-content', mode: 'read' });
  } catch {
    return undefined;
  }
  try {
    root = await root.getDirectoryHandle('Content');
  } catch {
    // the Content folder itself
  }

  const art: GameArt = { vars: {}, sheets: [] };
  const [cursors, menu, textBox, objects] = await Promise.all([
    bitmap(root, 'LooseSprites/Cursors.png'),
    bitmap(root, 'Maps/MenuTiles.png'),
    bitmap(root, 'LooseSprites/textBox.png'),
    openFile(root, 'Maps/springobjects.png')
  ]);

  if (cursors) {
    art.sheets.push('cursors');
    Object.assign(art.vars, {
      '--art-button': crop(cursors, [432, 439, 9, 9]),
      '--art-dropdown': crop(cursors, [433, 451, 3, 3]),
      '--art-dropdown-button': crop(cursors, [437, 450, 10, 11]),
      '--art-check': crop(cursors, [227, 425, 9, 9]),
      '--art-checked': crop(cursors, [236, 425, 9, 9]),
      '--art-slider': crop(cursors, [403, 383, 6, 6]),
      '--art-knob': crop(cursors, [420, 441, 10, 6]),
      '--art-scroll': compose(cursors, 25, 18, [[[325, 318, 12, 18], 0, 0, 12, 18], [[337, 318, 1, 18], 12, 0, 1, 18], [[338, 318, 12, 18], 13, 0, 12, 18]])
    });
  }
  if (menu) {
    art.sheets.push('menu');
    const tile = (x: number, y: number): Rect => [x, y, 64, 64];
    Object.assign(art.vars, {
      '--art-panel': crop(menu, [0, 256, 60, 60]),
      '--art-frame': compose(menu, 192, 192, [
        [tile(0, 0), 0, 0, 64, 64], [tile(128, 0), 64, 0, 64, 64], [tile(192, 0), 128, 0, 64, 64],
        [tile(0, 128), 0, 64, 64, 64], [tile(192, 128), 128, 64, 64, 64],
        [tile(0, 192), 0, 128, 64, 64], [tile(128, 192), 64, 128, 64, 64], [tile(192, 192), 128, 128, 64, 64]
      ]),
      '--art-frame-bg': crop(menu, tile(64, 128))
    });
  }
  if (textBox) {
    art.sheets.push('textbox');
    const h = textBox.height;
    art.vars['--art-textbox'] = compose(textBox, 36, h, [
      [[0, 0, 16, h], 0, 0, 16, h], [[16, 0, 4, h], 16, 0, 4, h], [[textBox.width - 16, 0, 16, h], 20, 0, 16, h]
    ]);
  }
  if (objects) {
    const sheet = await createImageBitmap(objects);
    art.objects = { url: URL.createObjectURL(objects), columns: Math.max(1, Math.floor(sheet.width / 16)), rows: Math.max(1, Math.floor(sheet.height / 16)) };
  }
  return art;
}

/** Releases the blob URL a GameArt holds. */
export function releaseGameArt(art: GameArt | undefined): void {
  if (art?.objects) {
    URL.revokeObjectURL(art.objects.url);
  }
}
