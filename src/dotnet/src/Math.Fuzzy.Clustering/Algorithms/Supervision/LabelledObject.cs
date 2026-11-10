namespace MathFuzzy.Clustering.Algorithms.Supervision;

/// <summary>
/// A labelled object of the partially supervised algorithms (Viattchenin 2013, sec. 2.2.2):
/// the object with the number <paramref name="ObjectIndex"/> (0-based) must belong to the
/// cluster of its class with a membership degree not below <paramref name="Membership"/>.
/// </summary>
/// <param name="ObjectIndex">0-based number of the object.</param>
/// <param name="Membership">Least admissible membership degree y in its cluster, in (0, 1].</param>
/// <param name="Class">
/// The class of the object. Objects with the same class must lie in one cluster, objects of
/// different classes in different clusters. Null (the default) = a class of its own, which is the
/// form of the monograph: c labelled objects, one per class.
/// </param>
public sealed record LabelledObject(int ObjectIndex, double Membership, int? Class = null);

/// <summary>
/// A labelled object of D-AIFC-PS(c) (Kacprzyk, Owsiński, Shyrai, Szmidt and Viattchenin 2017):
/// the object must belong to the cluster of its class with a membership degree not below
/// <paramref name="Membership"/> (y) and a non-membership degree not above
/// <paramref name="NonMembership"/> (f), where 0 ≤ y + f ≤ 1.
/// </summary>
/// <param name="ObjectIndex">0-based number of the object.</param>
/// <param name="Membership">Least admissible membership degree y, in (0, 1].</param>
/// <param name="NonMembership">Largest admissible non-membership degree f, in [0, 1).</param>
/// <param name="Class">As in <see cref="LabelledObject.Class"/>.</param>
public sealed record IntuitionisticLabelledObject(int ObjectIndex, double Membership, double NonMembership, int? Class = null);
