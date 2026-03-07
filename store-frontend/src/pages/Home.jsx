import { Link } from "react-router-dom";

export default function Home() {
  const categories = [
    { name: "T-Shirts", hint: "Everyday comfort" },
    { name: "Shirts & Tops", hint: "Smart + trendy" },
    { name: "Jeans", hint: "Perfect fit" },
    { name: "Kurtis", hint: "Ethnic style" },
  ];

  return (
    <>
      <section className="hero">
        <div className="heroCard">
          <h1 className="heroTitle">
            New Arrivals <br />
            <span>Womens Collection</span>
          </h1>

          <p className="heroText">
            Premium styles, trending designs, and comfy fits built for daily wear and party looks.
            Upgrade your wardrobe today.
          </p>

          <div className="heroActions">
            <Link to="/shop" className="btn btnPrimary">
              Shop Now
            </Link>
            <Link to="/offers" className="btn btnGhost">
              Explore Offers
            </Link>
          </div>
        </div>
      </section>

      <section className="section">
        <div className="container">
          <h2 className="sectionTitle">Categories</h2>

          <div className="catGrid">
            {categories.map((c) => (
              <div key={c.name} className="catCard">
                <div className="catName">{c.name}</div>
                <div className="catHint">{c.hint}</div>
              </div>
            ))}
          </div>
        </div>
      </section>
    </>
  );
}
