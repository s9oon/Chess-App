import { colorOf, type PieceChar } from './fen'

// Solid glyphs for both sides; CSS colours them. ︎ stops ♟ turning into an emoji on some systems.
const glyphs: Record<string, string> = {
  p: '♟︎', n: '♞', b: '♝', r: '♜', q: '♛', k: '♚',
}

export function Piece({ piece }: { piece: PieceChar }) {
  return <span className={`piece ${colorOf(piece)}`}>{glyphs[piece.toLowerCase()]}</span>
}
