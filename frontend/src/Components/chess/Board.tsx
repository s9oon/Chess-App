import { useState } from 'react'
import { parseFen, squareName, type Color } from './fen'
import { Square } from './Square'
import '../../Styles/Board.css'

type Props = {
  fen: string
  legalMoves: string[]       // UCI, from the server: ["e2e4", "g1f3", "e7e8q", ...]
  orientation: Color         // whose side is at the bottom
  interactive: boolean       // false = just draws (game over, etc.)
  onMove: (uci: string) => void
}

// Draws the position and turns two clicks into a UCI move. No chess rules here:
// what's legal comes from legalMoves, and the server checks it again in MovePiece.
export function Board({ fen, legalMoves, orientation, interactive, onMove }: Props) {
  const squares = parseFen(fen)

  // Remember which FEN the selection was made on, so it clears itself when a new position arrives.
  const [selection, setSelection] = useState<{ fen: string; square: number } | null>(null)
  const selected = selection?.fen === fen ? selection.square : null

  const movesFrom = (square: number) => legalMoves.filter((m) => m.startsWith(squareName(square)))
  const targets = selected === null
    ? new Set<string>()
    : new Set(movesFrom(selected).map((m) => m.slice(2, 4)))   // "e2e4" -> "e4"

  function click(square: number) {
    if (!interactive) return

    // Second click on a highlighted square = make the move.
    if (selected !== null && targets.has(squareName(square))) {
      const uci = squareName(selected) + squareName(square)
      const matches = legalMoves.filter((m) => m.startsWith(uci))
      // Promotions come as e7e8q/r/b/n; always pick the queen for now.
      onMove(matches.find((m) => m.endsWith('q')) ?? matches[0])
      setSelection(null)
      return
    }

    // Otherwise select this square if it has moves (only the side to move has any), else clear.
    setSelection(movesFrom(square).length > 0 ? { fen, square } : null)
  }

  // Rows top to bottom. White at the bottom: rank 8 first, a->h. Black: rank 1 first, h->a.
  const order: number[] = []
  for (let row = 0; row < 8; row++)
    for (let col = 0; col < 8; col++)
      order.push(orientation === 'white' ? (7 - row) * 8 + col : row * 8 + (7 - col))

  return (
    <div className="board">
      {order.map((sq) => (
        <Square
          key={sq}
          square={sq}
          piece={squares[sq]}
          selected={sq === selected}
          target={targets.has(squareName(sq))}
          onClick={() => click(sq)}
        />
      ))}
    </div>
  )
}
