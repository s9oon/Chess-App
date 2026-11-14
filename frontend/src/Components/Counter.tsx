type Props = {
  value: number
}

// Just shows the number. The value comes from the server (JoinMatch / CounterUpdated).
export function Counter({ value }: Props) {
  return <p className="counter">{value}</p>
}
