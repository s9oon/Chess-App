// Reading the FEN the server sends. Square numbers match backend/Chess/Move.cs:
//   index = rank * 8 + file     a1 = 0, h1 = 7, a2 = 8, e2 = 12, ... h8 = 63

export type PieceChar = 'P' | 'N' | 'B' | 'R' | 'Q' | 'K' | 'p' | 'n' | 'b' | 'r' | 'q' | 'k'
export type Color = 'white' | 'black'

// "rnbqkbnr/pppppppp/8/... w KQkq - 0 1" -> 64 entries, null = empty square.
// The placement field starts at rank 8, so the first '/' chunk is rank index 7.
export function parseFen(fen: string): (PieceChar | null)[] {
  const squares: (PieceChar | null)[] = Array(64).fill(null)
  const ranks = fen.split(' ')[0].split('/')

  ranks.forEach((chunk, i) => {
    const rank = 7 - i
    let file = 0
    for (const ch of chunk) {
      if (ch >= '1' && ch <= '8') file += Number(ch)      // "3" = 3 empty squares
      else squares[rank * 8 + file++] = ch as PieceChar
    }
  })

  return squares
}

// Second field: "w" or "b".
export function sideToMove(fen: string): Color {
  return fen.split(' ')[1] === 'w' ? 'white' : 'black'
}

// Uppercase = White.
export function colorOf(piece: PieceChar): Color {
  return piece === piece.toUpperCase() ? 'white' : 'black'
}

// 12 -> "e2"
export function squareName(square: number): string {
  return 'abcdefgh'[square % 8] + (Math.floor(square / 8) + 1)
}
