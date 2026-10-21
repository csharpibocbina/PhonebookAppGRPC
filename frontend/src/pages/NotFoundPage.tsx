import { Link } from "react-router";
import { Button } from "@/components/ui/button";

export function NotFoundPage() {
  return (
    <div className="bg-aurora grid min-h-dvh place-items-center px-4 text-center">
      <div>
        <p className="text-sm font-semibold text-primary">gRPC status: NOT_FOUND</p>
        <h1 className="mt-2 text-4xl font-extrabold">Page not found</h1>
        <p className="mt-2 text-muted-foreground">The page you're looking for doesn't exist.</p>
        <Link to="/" className="mt-6 inline-block">
          <Button>Back home</Button>
        </Link>
      </div>
    </div>
  );
}
