import { Piece } from './Piece'
import type { PieceChar } from './fen'

type Props = {
  square: number             // 0..63, a1 = 0
  piece: PieceChar | null
  selected: boolean          // the piece you clicked
  target: boolean            // somewhere the selected piece can go
  onClick: () => void
}

// One cell. a1 is dark: (file + rank) even = dark.
export function Square({ square, piece, selected, target, onClick }: Props) {
  const dark = ((square % 8) + Math.floor(square / 8)) % 2 === 0
  const classes = ['square', dark ? 'dark' : 'light', selected && 'selected', target && 'target']
    .filter(Boolean).join(' ')

  return (
    <button className={classes} onClick={onClick}>
      {piece && <Piece piece={piece} />}
    </button>
  )
}
