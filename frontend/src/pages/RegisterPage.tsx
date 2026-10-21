import { useState } from "react";
import { Link, useNavigate } from "react-router";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { errorMessage, getFieldErrors } from "@/api/errors";
import { useAuth } from "@/auth/AuthContext";
import { Button } from "@/components/ui/button";
import { FieldError, Input, Label } from "@/components/ui/input";
import { AuthLayout } from "./AuthLayout";
import { PasswordInput } from "./PasswordInput";

// Same rules as RegisterCommandValidator on the server.
const schema = z.object({
  displayName: z.string().trim().min(1, "Tell us your name.").max(100),
  email: z.email("Enter a valid email address."),
  password: z
    .string()
    .min(8, "At least 8 characters.")
    .regex(/[A-Z]/, "Add an uppercase letter.")
    .regex(/[a-z]/, "Add a lowercase letter.")
    .regex(/[0-9]/, "Add a digit."),
});
type Values = z.infer<typeof schema>;

export function RegisterPage() {
  const { register: signUp } = useAuth();
  const navigate = useNavigate();
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { displayName: "", email: "", password: "" } });

  const onSubmit = handleSubmit(async ({ displayName, email, password }) => {
    setServerError(null);
    try {
      await signUp(email, password, displayName);
      navigate("/contacts", { replace: true });
    } catch (err) {
      const fieldErrors = Object.entries(getFieldErrors(err));
      if (fieldErrors.length) fieldErrors.forEach(([k, v]) => setError(k as keyof Values, { message: v[0] }));
      else setServerError(errorMessage(err));
    }
  });

  return (
    <AuthLayout
      title="Create your account"
      subtitle="A welcome email will land in your PaperCut inbox."
      footer={
        <>
          Already have an account?{" "}
          <Link to="/login" className="font-medium text-primary hover:underline">
            Sign in
          </Link>
        </>
      }
    >
      <form onSubmit={onSubmit} noValidate className="space-y-4">
        {serverError && (
          <div role="alert" className="rounded-xl border border-danger/30 bg-danger/10 px-3.5 py-2.5 text-sm text-danger">
            {serverError}
          </div>
        )}
        <div>
          <Label htmlFor="displayName">Name</Label>
          <Input id="displayName" autoComplete="name" placeholder="Jay" aria-invalid={!!errors.displayName || undefined} {...register("displayName")} />
          <FieldError message={errors.displayName?.message} />
        </div>
        <div>
          <Label htmlFor="email">Email</Label>
          <Input id="email" type="email" autoComplete="email" placeholder="you@example.com" aria-invalid={!!errors.email || undefined} {...register("email")} />
          <FieldError message={errors.email?.message} />
        </div>
        <div>
          <Label htmlFor="password">Password</Label>
          <PasswordInput id="password" autoComplete="new-password" placeholder="At least 8 characters" aria-invalid={!!errors.password || undefined} {...register("password")} />
          <FieldError message={errors.password?.message} />
        </div>
        <Button type="submit" className="w-full" size="lg" loading={isSubmitting}>
          Create account
        </Button>
      </form>
    </AuthLayout>
  );
}
