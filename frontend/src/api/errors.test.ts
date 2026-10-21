import { Code, ConnectError } from "@connectrpc/connect";
import { errorMessage, getFieldErrors } from "./errors";

describe("gRPC error helpers", () => {
  it("reads field errors from the validation-errors trailer", () => {
    const err = new ConnectError("bad", Code.InvalidArgument, { "validation-errors": JSON.stringify({ firstName: ["Required."] }) });

    expect(getFieldErrors(err)).toEqual({ firstName: ["Required."] });
  });

  it("ignores trailers on other status codes", () => {
    const err = new ConnectError("nope", Code.NotFound, { "validation-errors": "{}" });

    expect(getFieldErrors(err)).toEqual({});
  });

  it("turns status codes into friendly messages", () => {
    expect(errorMessage(new ConnectError("x", Code.Unavailable))).toMatch(/can't reach/i);
    expect(errorMessage(new ConnectError("Invalid email or password.", Code.Unauthenticated))).toBe("Invalid email or password.");
  });
});
